namespace HotelBooking.Application.Hotels.Dtos;

public sealed record HotelSearchRequest(
    Guid? CityId = null,
    DateOnly? CheckIn = null,
    DateOnly? CheckOut = null,
    int? Adults = null,
    int? Children = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    int[]? Stars = null,
    string? RoomType = null,
    string? Sort = null,
    int? Page = null,
    int? PageSize = null);
