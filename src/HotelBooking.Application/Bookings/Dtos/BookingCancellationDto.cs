using HotelBooking.Application.Payments.Dtos;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Payments;

namespace HotelBooking.Application.Bookings.Dtos;

public sealed record BookingCancellationDto(
    Guid BookingId,
    string ConfirmationNumber,
    string Status,
    string Reason,
    DateTimeOffset CancelledAtUtc,
    int NightsReleased,
    RefundDto? Refund)
{
    public static BookingCancellationDto From(Booking booking, int nightsReleased, Payment? payment) => new(
        booking.Id,
        booking.Confirmation.Value,
        booking.Status.ToString(),
        booking.CancellationReason?.ToString() ?? string.Empty,
        booking.CancelledAtUtc ?? default,
        nightsReleased,
        payment is null ? null : RefundDto.From(payment));
}
