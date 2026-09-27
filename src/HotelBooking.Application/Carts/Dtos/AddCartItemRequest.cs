namespace HotelBooking.Application.Carts.Dtos;

public sealed record AddCartItemRequest(
    Guid RoomId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children);
