using HotelBooking.Application.Common;
using HotelBooking.Application.Rooms.Dtos;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Rooms;

public interface IRoomAvailabilityService
{
    Task<Result<PagedList<AvailableRoomDto>>> ListForHotelAsync(
        Guid hotelId,
        HotelRoomsRequest request,
        CancellationToken cancellationToken = default);
}
