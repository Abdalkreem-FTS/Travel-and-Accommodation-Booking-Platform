using HotelBooking.Application.Bookings.Dtos;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Bookings;

public interface IBookingCancellationService
{
    Task<Result<BookingCancellationDto>> CancelAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
