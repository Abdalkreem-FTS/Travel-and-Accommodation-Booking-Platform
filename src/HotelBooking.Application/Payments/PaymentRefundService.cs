using HotelBooking.Application.Abstractions;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Payments;
using HotelBooking.Domain.Results;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Payments;

public sealed class PaymentRefundService(
    IPaymentProvider paymentProvider,
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    ILogger<PaymentRefundService> logger) : IPaymentRefundService
{
    private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(15);

    public async Task<int> SendPendingAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var payments = await paymentRepository.ListRefundingAsync(batchSize, cancellationToken);

        var sent = 0;

        foreach (var payment in payments)
        {
            var result = await RefundAsync(payment.Id, cancellationToken);

            if (result.IsError)
            {
                logger.LogWarning(
                    "The refund of payment {PaymentId}, pending since {RequestedAtUtc}, is still pending "
                    + "({ErrorCode}); the next run of the unfinished payments worker asks again",
                    payment.Id, payment.RefundRequestedAtUtc, result.TopError.Code);
            }

            sent++;
        }

        return sent;
    }

    public async Task<Result<Success>> RefundAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await paymentRepository.GetAsync(paymentId, cancellationToken);

        if (payment?.RefundRequestedAtUtc is null)
        {
            return PaymentErrors.RefundNotFound;
        }

        if (payment.Status is not PaymentStatus.Refunding)
        {
            logger.LogInformation(
                "The refund of payment {PaymentId} already ended as {PaymentStatus}; nothing to send",
                paymentId, payment.Status);

            return Result.Success;
        }

        var sent = await SendRefundAsync(payment, cancellationToken);

        if (sent.IsError && sent.TopError != PaymentErrors.RefundRejected)
        {
            return sent.Errors;
        }

        var nowUtc = dateTimeProvider.UtcNow;

        var recorded = sent switch
        {
            { IsError: true } => payment.FailRefund(nowUtc),
            { Value.Settled: true } => payment.CompleteRefund(sent.Value.Id, nowUtc),
            _ => payment.RecordRefundSent(sent.Value.Id)
        };

        if (recorded.IsError)
        {
            return recorded.Errors;
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        if (sent is { IsSuccess: true, Value.Settled: false })
        {
            logger.LogInformation(
                "The payment provider accepted the refund of payment {PaymentId} as {ProviderRefundId} "
                + "and will report when it settles",
                paymentId, sent.Value.Id);
        }
        else if (sent.IsSuccess)
        {
            Telemetry.Refunds.Add(1, new KeyValuePair<string, object?>("status", "succeeded"));

            logger.LogInformation(
                "Refunded {Amount} {Currency} of payment {PaymentId} as provider refund {ProviderRefundId}",
                payment.Amount.Amount, payment.Amount.Currency, paymentId, sent.Value.Id);
        }
        else
        {
            Telemetry.Refunds.Add(1, new KeyValuePair<string, object?>("status", "failed"));

            logger.LogError(
                "The payment provider refused to refund {Amount} {Currency} of payment {PaymentId} for "
                + "booking {BookingId}; the guest is owed that money and it must be returned by hand",
                payment.Amount.Amount, payment.Amount.Currency, paymentId, payment.BookingId);
        }

        return Result.Success;
    }

    private async Task<Result<ProviderRefund>> SendRefundAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(ProviderTimeout);

        try
        {
            var sent = await paymentProvider.RefundAsync(payment, budget.Token);

            if (sent.IsError && sent.TopError != PaymentErrors.RefundRejected)
            {
                logger.LogWarning(
                    "The refund of payment {PaymentId} could not be sent ({ErrorCode}); it will be retried",
                    payment.Id, sent.TopError.Code);
            }

            return sent;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception, "The refund of payment {PaymentId} could not be sent; it will be retried",
                payment.Id);

            return PaymentErrors.ProviderUnavailable;
        }
    }
}
