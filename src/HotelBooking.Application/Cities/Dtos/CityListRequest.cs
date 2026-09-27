namespace HotelBooking.Application.Cities.Dtos;

public sealed record CityListRequest(
    string? Search = null,
    string? Sort = null,
    int? Page = null,
    int? PageSize = null);
