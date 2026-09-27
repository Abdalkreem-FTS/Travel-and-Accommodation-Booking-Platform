namespace HotelBooking.Application.Cities.Dtos;

public sealed record CitySummaryDto(
    Guid Id,
    string Name,
    string Country,
    string PostOffice,
    string? ThumbnailUrl,
    int HotelCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ModifiedAtUtc,
    string Version);
