using HotelBooking.Application.Common;
using HotelBooking.Application.Rooms.Dtos;

namespace HotelBooking.Application.Rooms;

public interface IRoomQueries
{
    Task<PagedList<AvailableRoomDto>> ListAvailableAsync(
        RoomAvailabilityCriteria criteria,
        CancellationToken cancellationToken = default);
}
