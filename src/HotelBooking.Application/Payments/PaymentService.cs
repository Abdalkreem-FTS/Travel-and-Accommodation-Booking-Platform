using HotelBooking.Application.Abstractions;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Payments;
using HotelBooking.Domain.Results;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Payments;

public sealed class PaymentService(
    IPaymentProvider paymentProvider,
    IPaymentRepository paymentRepository,
    IBookingRepository bookingRepository,
    IUnitOfWork unitOfWork,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ILogger<PaymentService> logger) : IPaymentService
{
    private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(15);

    private enum Outcome
    {
        Applied,
        Ignored,
        Stale,
        Unknown,
        RefundingLatePayment,
        Unreconciled
    }

    public async Task<Result<Success>> HandleEventAsync(
        string payload,
        string? signature,
        CancellationToken cancellationToken = default)
    {
        var read = paymentProvider.ReadEvent(payload, signature);

        if (read.IsError)
        {
            logger.LogWarning("Refused a payment event ({ErrorCode})", read.TopError.Code);

            Count("unreadable", "rejected");

            return read.Errors;
        }

        var paymentEvent = read.Value;

        if (paymentEvent is UnhandledPaymentEvent)
        {
            Count(paymentEvent.Type, Outcome.Ignored);

            return Result.Success;
        }

        var applied = await unitOfWork.ExecuteInTransactionAsync(
            async token => await ApplyAsync(paymentEvent, token),
            cancellationToken);

        switch (applied.IsError)
        {
            case true:
                logger.LogError(
                    "Payment event {EventId} ({EventType}) could not be applied ({ErrorCode}); the provider "
                    + "will deliver it again",
                    paymentEvent.Id, paymentEvent.Type, applied.TopError.Code);

                Count(paymentEvent.Type, "failed");

                return applied.Errors;
            default:
                Count(paymentEvent.Type, applied.Value);

                return Result.Success;
        }
    }

    public async Task<int> ExpireOverdueAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var overdue = await paymentRepository.ListOverdueAsync(dateTimeProvider.UtcNow, batchSize, cancellationToken);

        foreach (var payment in overdue)
        {
            await SettleOverdueAsync(payment, cancellationToken);
        }

        return overdue.Count;
    }

    private async Task SettleOverdueAsync(Payment overdue, CancellationToken cancellationToken)
    {
        var expiry = await ExpireCheckoutAsync(overdue, cancellationToken);

        if (expiry is null)
        {
            return;
        }

        var settled = await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var payment = await paymentRepository.GetAsync(overdue.Id, token)
                    ?? throw new InvalidOperationException($"Overdue payment {overdue.Id} is gone.");

                var booking = await bookingRepository.GetWithNightsAsync(payment.BookingId, token)
                    ?? throw new InvalidOperationException($"Payment {payment.Id} names a booking that is not there.");

                var outcome = expiry is CheckoutAlreadyPaid paid
                    ? Complete(paid.ProviderPaymentId, paid.AmountReceived, payment, booking)
                    : Expire(payment, booking, "overdue");

                if (outcome.IsError || !IsChange(outcome.Value))
                {
                    return outcome;
                }

                var saved = await unitOfWork.SaveChangesAsync(token);

                return saved.IsError ? saved.Errors : outcome;
            },
            cancellationToken);

        if (settled.IsError)
        {
            logger.LogWarning(
                "Overdue payment {PaymentId} could not be settled ({ErrorCode}); the next run of the unfinished payments worker tries again",
                overdue.Id, settled.TopError.Code);
        }
    }

    private async Task<CheckoutExpiry?> ExpireCheckoutAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (payment.ProviderCheckoutId is not { } checkoutId)
        {
            return new CheckoutExpiredUnpaid();
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(ProviderTimeout);

        try
        {
            var closed = await paymentProvider.ExpireCheckoutAsync(checkoutId, budget.Token);

            if (closed.IsSuccess)
            {
                return closed.Value;
            }

            logger.LogWarning(
                "The payment provider would not close checkout {CheckoutId} of payment {PaymentId} ({ErrorCode}); "
                + "its nights stay held until the next run of the unfinished payments worker",
                checkoutId, payment.Id, closed.TopError.Code);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "The payment provider could not be asked to close checkout {CheckoutId} of payment {PaymentId}; "
                + "its nights stay held until the next run of the unfinished payments worker",
                checkoutId, payment.Id);
        }

        return null;
    }

    private async Task<Result<Outcome>> ApplyAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken)
    {
        var outcome = paymentEvent is RefundSettled settled
            ? await SettleRefundAsync(settled, cancellationToken)
            : await SettleCheckoutAsync(paymentEvent, cancellationToken);

        if (outcome.IsError || !IsChange(outcome.Value))
        {
            return outcome;
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        return saved.IsError ? saved.Errors : outcome;
    }

    private async Task<Result<Outcome>> SettleCheckoutAsync(
        PaymentEvent paymentEvent,
        CancellationToken cancellationToken)
    {
        var checkoutId = paymentEvent switch
        {
            CheckoutCompleted completed => completed.CheckoutId,
            CheckoutExpired expired => expired.CheckoutId,
            _ => throw new InvalidOperationException($"Payment event type {paymentEvent.Type} has no handler.")
        };

        var payment = await paymentRepository.GetByCheckoutIdAsync(checkoutId, cancellationToken);

        if (payment is null)
        {
            logger.LogWarning(
                "Payment event {EventId} names checkout {CheckoutId}, which no payment holds; skipped",
                paymentEvent.Id, checkoutId);

            return Outcome.Unknown;
        }

        var booking = await bookingRepository.GetWithNightsAsync(payment.BookingId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {payment.Id} names a booking that is not there.");

        return paymentEvent is CheckoutCompleted paid
            ? Complete(paid.ProviderPaymentId, paid.AmountReceived, payment, booking)
            : Expire(payment, booking, "checkout_expired");
    }

    private async Task<Result<Outcome>> SettleRefundAsync(RefundSettled settled, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetAsync(settled.PaymentId, cancellationToken);

        if (payment is null)
        {
            logger.LogWarning(
                "Payment event {EventId} names payment {PaymentId}, which does not exist; skipped",
                settled.Id, settled.PaymentId);

            return Outcome.Unknown;
        }

        var nowUtc = dateTimeProvider.UtcNow;

        var recorded = settled.Succeeded
            ? payment.CompleteRefund(settled.ProviderRefundId, nowUtc)
            : payment.FailRefund(nowUtc);

        if (recorded.IsError)
        {
            return Outcome.Stale;
        }

        Telemetry.Refunds.Add(
            1, new KeyValuePair<string, object?>("status", settled.Succeeded ? "succeeded" : "failed"));

        if (settled.Succeeded)
        {
            logger.LogInformation(
                "The refund of payment {PaymentId} settled as provider refund {ProviderRefundId}",
                payment.Id, settled.ProviderRefundId);
        }
        else
        {
            logger.LogError(
                "The payment provider failed refund {ProviderRefundId} of payment {PaymentId} for "
                + "booking {BookingId}; the guest is owed that money and it must be returned by hand",
                settled.ProviderRefundId, payment.Id, payment.BookingId);
        }

        return Outcome.Applied;
    }

    private Result<Outcome> Complete(
        string providerPaymentId,
        Money amountReceived,
        Payment payment,
        Booking booking)
    {
        var nowUtc = dateTimeProvider.UtcNow;

        var succeeded = payment.MarkSucceeded(providerPaymentId, amountReceived, nowUtc);

        if (succeeded.IsError)
        {
            if (payment.ProviderPaymentId == providerPaymentId)
            {
                return Outcome.Stale;
            }

            if (payment.AcceptLatePayment(providerPaymentId, amountReceived, nowUtc).IsSuccess)
            {
                return RefundLatePayment(payment, booking, providerPaymentId);
            }

            logger.LogError(
                "Provider payment {ProviderPaymentId} took {Amount} {Currency} for payment {PaymentId} of "
                + "booking {BookingId}, but that payment is {PaymentStatus} ({ErrorCode}); the money is "
                + "held for no booking and must be refunded",
                providerPaymentId, amountReceived.Amount, amountReceived.Currency,
                payment.Id, booking.Id, payment.Status, succeeded.TopError.Code);

            return Outcome.Unreconciled;
        }

        var confirmed = booking.Confirm(guidProvider.NewSortable(), nowUtc);

        if (confirmed.IsError)
        {
            return confirmed.Errors;
        }

        Telemetry.PaymentsSucceeded.Add(1);
        Telemetry.PaymentTimeToPay.Record((nowUtc - booking.CreatedAtUtc).TotalSeconds);

        logger.LogInformation(
            "Confirmed booking {BookingId}: payment {PaymentId} succeeded as provider payment {ProviderPaymentId}",
            booking.Id, payment.Id, providerPaymentId);

        return Outcome.Applied;
    }

    private Result<Outcome> RefundLatePayment(Payment payment, Booking booking, string providerPaymentId)
    {
        var refund = payment.RefundInFull(dateTimeProvider.UtcNow);

        if (refund.IsError)
        {
            return refund.Errors;
        }

        Telemetry.Refunds.Add(1, new KeyValuePair<string, object?>("status", "requested"));

        logger.LogWarning(
            "Provider payment {ProviderPaymentId} arrived after payment {PaymentId} expired and booking "
            + "{BookingId} released its nights; refunding it in full",
            providerPaymentId, payment.Id, booking.Id);

        return Outcome.RefundingLatePayment;
    }

    private Result<Outcome> Expire(Payment payment, Booking booking, string reason)
    {
        if (payment.Expire(dateTimeProvider.UtcNow).IsError)
        {
            return Outcome.Stale;
        }

        var nightsHeld = booking.Nights.Count;

        var expired = booking.Expire();

        if (expired.IsError)
        {
            return expired.Errors;
        }

        Telemetry.PaymentsExpired.Add(1, new KeyValuePair<string, object?>("reason", reason));

        logger.LogInformation(
            "Expired booking {BookingId} and released {NightCount} room-night(s): payment {PaymentId} "
            + "was never completed",
            booking.Id, nightsHeld, payment.Id);

        return Outcome.Applied;
    }

    private static bool IsChange(Outcome outcome) => outcome is Outcome.Applied or Outcome.RefundingLatePayment;

    private static void Count(string type, Outcome outcome) =>
        Count(type, outcome switch
        {
            Outcome.RefundingLatePayment => "refunding_late_payment",
            _ => outcome.ToString().ToLowerInvariant()
        });

    private static void Count(string type, string outcome) =>
        Telemetry.PaymentEvents.Add(
            1,
            new KeyValuePair<string, object?>("type", type),
            new KeyValuePair<string, object?>("outcome", outcome));
}
