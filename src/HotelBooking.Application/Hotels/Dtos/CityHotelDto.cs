namespace HotelBooking.Application.Hotels.Dtos;

public sealed record CityHotelDto(
    Guid Id,
    string Name,
    int StarRating,
    string? ThumbnailUrl,
    int RoomCount);
