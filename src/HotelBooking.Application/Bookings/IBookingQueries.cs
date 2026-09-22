using HotelBooking.Application.Bookings.Dtos;

namespace HotelBooking.Application.Bookings;

public interface IBookingQueries
{
    Task<BookingConfirmationDto?> GetConfirmationAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);
}
