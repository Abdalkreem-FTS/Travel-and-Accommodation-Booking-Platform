namespace HotelBooking.Application.Rooms.Dtos;

public sealed record UpdateRoomRequest(
    string? Number,
    int Type,
    int Adults,
    int Children,
    decimal BasePrice,
    string? Currency);
