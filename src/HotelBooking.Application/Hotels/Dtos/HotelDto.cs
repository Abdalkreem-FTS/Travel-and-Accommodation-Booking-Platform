using HotelBooking.Application.Amenities.Dtos;
using HotelBooking.Application.Common;
using HotelBooking.Domain.Hotels;

namespace HotelBooking.Application.Hotels.Dtos;

public sealed record HotelDto(
    Guid Id,
    Guid CityId,
    string Name,
    string Description,
    string Owner,
    int StarRating,
    decimal Latitude,
    decimal Longitude,
    string? ThumbnailUrl,
    IReadOnlyList<HotelImageDto> Images,
    IReadOnlyList<AmenityDto> Amenities,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ModifiedAtUtc,
    string Version)
{
    public static HotelDto From(
        Hotel hotel,
        IReadOnlyList<AmenityDto> amenities,
        ConcurrencyToken version) => new(
        hotel.Id,
        hotel.CityId,
        hotel.Name,
        hotel.Description,
        hotel.Owner,
        hotel.StarRating.Value,
        hotel.Location.Latitude,
        hotel.Location.Longitude,
        hotel.ThumbnailUrl,
        [.. hotel.Images.Select(HotelImageDto.From)],
        amenities,
        hotel.CreatedAtUtc,
        hotel.ModifiedAtUtc,
        version.Version);
}
