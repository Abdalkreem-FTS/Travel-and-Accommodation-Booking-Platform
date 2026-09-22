using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Bookings.Dtos;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Results;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Bookings;

public sealed class BookingCancellationService(
    IBookingRepository bookingRepository,
    IUnitOfWork unitOfWork,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ILogger<BookingCancellationService> logger) : IBookingCancellationService
{
    public async Task<Result<BookingCancellationDto>> CancelAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
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

                var saved = await unitOfWork.SaveChangesAsync(token);

                return saved.IsError
                    ? saved.Errors
                    : Result<BookingCancellationDto>.From(
                        BookingCancellationDto.From(booking, nightsHeld));
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

        logger.LogInformation(
            "Cancelled booking {BookingId} for user {UserId} and released {NightCount} room-night(s)",
            bookingId, userId, result.Value.NightsReleased);

        return result;
    }

    public async Task<Result<Updated>> VoidForFailedPaymentAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var result = await unitOfWork.ExecuteInTransactionAsync<Updated>(
            async token =>
            {
                var booking = await bookingRepository.GetWithNightsAsync(bookingId, token);

                if (booking is null)
                {
                    return BookingErrors.NotFound;
                }

                var voided = booking.VoidForFailedPayment(
                    guidProvider.NewSortable(), dateTimeProvider.UtcNow);

                if (voided.IsError)
                {
                    return voided.Errors;
                }

                var saved = await unitOfWork.SaveChangesAsync(token);

                return saved.IsError ? saved.Errors : Result.Updated;
            },
            cancellationToken);

        if (result.IsError)
        {
            logger.LogError(
                "Booking {BookingId} could not be voided after its payment failed ({ErrorCode}); "
                + "its room-nights are still held and must be released",
                bookingId, result.TopError.Code);

            return result.Errors;
        }

        Telemetry.BookingsVoided.Add(1);

        logger.LogWarning(
            "Voided booking {BookingId} and released its nights: the payment could not be captured",
            bookingId);

        return Result.Updated;
    }
}
