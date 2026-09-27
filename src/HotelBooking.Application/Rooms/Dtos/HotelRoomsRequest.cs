namespace HotelBooking.Application.Rooms.Dtos;

public sealed record HotelRoomsRequest(
    DateOnly? CheckIn = null,
    DateOnly? CheckOut = null,
    int? Adults = null,
    int? Children = null,
    int? Page = null,
    int? PageSize = null);
