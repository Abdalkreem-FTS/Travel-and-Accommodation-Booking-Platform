namespace HotelBooking.Application.Hotels.Dtos;

public sealed record UpdateHotelRequest(
    Guid CityId,
    string? Name,
    string? Description,
    string? Owner,
    int StarRating,
    decimal Latitude,
    decimal Longitude,
    string? ThumbnailUrl,
    IReadOnlyList<HotelImageRequest>? Images = null,
    IReadOnlyList<Guid>? AmenityIds = null);
