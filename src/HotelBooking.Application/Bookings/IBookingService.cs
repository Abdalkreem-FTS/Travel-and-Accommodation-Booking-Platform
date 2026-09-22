using HotelBooking.Application.Bookings.Dtos;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Bookings;

public interface IBookingService
{
    Task<Result<BookingDto>> CreateAsync(
        CreateBookingRequest request,
        Guid userId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}
