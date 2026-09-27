using HotelBooking.Application.Common;
using HotelBooking.Domain.Cities;

namespace HotelBooking.Application.Cities.Dtos;

public sealed record CityDto(
    Guid Id,
    string Name,
    string Country,
    string PostOffice,
    string? ThumbnailUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ModifiedAtUtc,
    string Version)
{
    public static CityDto From(City city, ConcurrencyToken version) => new(
        city.Id,
        city.Name,
        city.Country.Value,
        city.PostOffice,
        city.ThumbnailUrl,
        city.CreatedAtUtc,
        city.ModifiedAtUtc,
        version.Version);
}
