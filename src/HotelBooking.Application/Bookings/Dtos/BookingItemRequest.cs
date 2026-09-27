namespace HotelBooking.Application.Bookings.Dtos;

public sealed record BookingItemRequest(
    Guid RoomId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children);
