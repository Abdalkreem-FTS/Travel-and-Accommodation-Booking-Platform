namespace HotelBooking.Application.Cities.Dtos;

public sealed record UpdateCityRequest(
    string? Name,
    string? Country,
    string? PostOffice,
    string? ThumbnailUrl);
