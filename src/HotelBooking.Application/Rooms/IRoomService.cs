using HotelBooking.Application.Rooms.Dtos;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Rooms;

public interface IRoomService
{
    Task<Result<RoomDto>> CreateAsync(
        Guid hotelId,
        CreateRoomRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RoomDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<RoomDto>> UpdateAsync(
        Guid id,
        UpdateRoomRequest request,
        string? ifMatch,
        CancellationToken cancellationToken = default);

    Task<Result<Deleted>> DeleteAsync(
        Guid id,
        string? ifMatch,
        CancellationToken cancellationToken = default);
}
