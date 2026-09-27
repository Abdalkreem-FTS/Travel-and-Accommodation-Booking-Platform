using HotelBooking.Application.Bookings.Dtos;
using HotelBooking.Application.Common;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Bookings;

public interface IBookingService
{
    Task<Result<BookingDto>> CreateAsync(
        CreateBookingRequest request,
        Guid userId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<Result<BookingDto>> GetAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default);

    Task<Result<PagedList<BookingSummaryDto>>> ListAsync(
        BookingListRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);
}
