namespace HotelBooking.Application.Rooms.Dtos;

public sealed record AvailableRoomDto(
    Guid Id,
    string Number,
    string Type,
    int Adults,
    int Children,
    decimal NightlyRate,
    string Currency,
    int? Nights = null,
    decimal? Total = null);
