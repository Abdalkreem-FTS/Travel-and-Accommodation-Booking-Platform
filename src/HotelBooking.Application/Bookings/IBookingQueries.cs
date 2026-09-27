using HotelBooking.Application.Bookings.Dtos;
using HotelBooking.Application.Common;

namespace HotelBooking.Application.Bookings;

public interface IBookingQueries
{
    Task<PagedList<BookingSummaryDto>> ListForUserAsync(
        Guid userId,
        PageRequest paging,
        CancellationToken cancellationToken = default);

    Task<BookingConfirmationDto?> GetConfirmationAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task<BookingCancellationNoticeDto?> GetCancellationNoticeAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);
}
