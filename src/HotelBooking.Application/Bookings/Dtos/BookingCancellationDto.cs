using HotelBooking.Domain.Bookings;

namespace HotelBooking.Application.Bookings.Dtos;

public sealed record BookingCancellationDto(
    Guid BookingId,
    string ConfirmationNumber,
    string Status,
    string Reason,
    DateTimeOffset CancelledAtUtc,
    int NightsReleased)
{
    public static BookingCancellationDto From(Booking booking, int nightsReleased) => new(
        booking.Id,
        booking.Confirmation.Value,
        booking.Status.ToString(),
        booking.CancellationReason?.ToString() ?? string.Empty,
        booking.CancelledAtUtc ?? default,
        nightsReleased);
}
