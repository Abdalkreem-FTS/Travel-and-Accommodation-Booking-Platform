using HotelBooking.Application.Common;
using HotelBooking.Domain.Rooms;

namespace HotelBooking.Application.Rooms.Dtos;

public sealed record RoomDto(
    Guid Id,
    Guid HotelId,
    string Number,
    string Type,
    int Adults,
    int Children,
    decimal BasePrice,
    string Currency,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ModifiedAtUtc,
    string Version)
{
    public static RoomDto From(Room room, ConcurrencyToken version) => new(
        room.Id,
        room.HotelId,
        room.Number,
        room.Type.ToString(),
        room.Capacity.Adults,
        room.Capacity.Children,
        room.BasePrice.Amount,
        room.BasePrice.Currency,
        room.CreatedAtUtc,
        room.ModifiedAtUtc,
        version.Version);
}
