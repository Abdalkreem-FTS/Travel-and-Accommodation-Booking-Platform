using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Bookings.Dtos;
using HotelBooking.Application.Payments;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Payments;
using HotelBooking.Domain.Results;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Bookings;

public sealed class BookingCancellationService(
    IBookingRepository bookingRepository,
    IPaymentRepository paymentRepository,
    IPaymentProvider paymentProvider,
    IUnitOfWork unitOfWork,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ILogger<BookingCancellationService> logger) : IBookingCancellationService
{
    private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(15);

    public async Task<Result<BookingCancellationDto>> CancelAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var closed = await CloseCheckoutAsync(bookingId, userId, cancellationToken);

        if (closed.IsError)
        {
            logger.LogInformation(
                "Refused to cancel booking {BookingId} for user {UserId} ({ErrorCode})",
                bookingId, userId, closed.TopError.Code);

            return closed.Errors;
        }

        var result = await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var booking = await bookingRepository.GetWithNightsAsync(bookingId, token);

                if (booking is null || booking.UserId != userId)
                {
                    return BookingErrors.NotFound;
                }

                var nightsHeld = booking.Nights.Count;

                var cancelled = booking.Cancel(guidProvider.NewSortable(), dateTimeProvider.UtcNow);

                if (cancelled.IsError)
                {
                    return cancelled.Errors;
                }

                var payment = await paymentRepository.GetForBookingAsync(bookingId, token);

                switch (payment)
                {
                    case { Status: PaymentStatus.Pending }:
                        payment.Expire(dateTimeProvider.UtcNow);
                        break;
                    case { Status: PaymentStatus.Succeeded }:
                        {
                            var refunded = payment.RefundInFull(dateTimeProvider.UtcNow);

                            if (refunded.IsError)
                            {
                                return refunded.Errors;
                            }

                            break;
                        }
                }

                var saved = await unitOfWork.SaveChangesAsync(token);

                return saved.IsError
                    ? saved.Errors
                    : Result<BookingCancellationDto>.From(
                        BookingCancellationDto.From(booking, nightsHeld, payment));
            },
            cancellationToken);

        if (result.IsError)
        {
            logger.LogInformation(
                "Refused to cancel booking {BookingId} for user {UserId} ({ErrorCode})",
                bookingId, userId, result.TopError.Code);

            return result.Errors;
        }

        Telemetry.BookingsCancelled.Add(1);

        if (result.Value.Refund is { } refund)
        {
            Telemetry.Refunds.Add(1, new KeyValuePair<string, object?>("status", "requested"));

            logger.LogInformation(
                "Cancelled booking {BookingId} for user {UserId}, released {NightCount} room-night(s) and "
                + "requested a refund of {Amount} {Currency}",
                bookingId, userId, result.Value.NightsReleased, refund.Amount, refund.Currency);

            return result;
        }

        logger.LogInformation(
            "Cancelled booking {BookingId} for user {UserId} and released {NightCount} room-night(s)",
            bookingId, userId, result.Value.NightsReleased);

        return result;
    }

    private async Task<Result<Success>> CloseCheckoutAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetWithLinesAsync(bookingId, cancellationToken);

        if (booking is null || booking.UserId != userId)
        {
            return BookingErrors.NotFound;
        }

        var allowed = booking.CanCancel(dateTimeProvider.UtcNow);

        if (allowed.IsError || booking.Status is not BookingStatus.Pending)
        {
            return allowed;
        }

        var payment = await paymentRepository.GetForBookingAsync(bookingId, cancellationToken);

        if (payment is not { Status: PaymentStatus.Pending, ProviderCheckoutId: { } checkoutId })
        {
            return Result.Success;
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(ProviderTimeout);

        try
        {
            var expiry = await paymentProvider.ExpireCheckoutAsync(checkoutId, budget.Token);

            if (expiry.IsError)
            {
                return PaymentErrors.ProviderUnavailable;
            }

            return expiry.Value is CheckoutAlreadyPaid ? BookingErrors.PaymentJustCompleted : Result.Success;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception, "Checkout {CheckoutId} of booking {BookingId} could not be closed", checkoutId, bookingId);

            return PaymentErrors.ProviderUnavailable;
        }
    }
}
