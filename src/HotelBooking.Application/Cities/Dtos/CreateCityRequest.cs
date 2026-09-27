namespace HotelBooking.Application.Cities.Dtos;

public sealed record CreateCityRequest(
    string? Name,
    string? Country,
    string? PostOffice,
    string? ThumbnailUrl);
