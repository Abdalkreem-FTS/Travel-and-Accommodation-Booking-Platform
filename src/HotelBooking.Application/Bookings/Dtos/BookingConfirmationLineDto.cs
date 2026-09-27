namespace HotelBooking.Application.Bookings.Dtos;

public sealed record BookingConfirmationLineDto(
    int LineNumber,
    string RoomNumber,
    string RoomType,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int Adults,
    int Children,
    decimal LineTotal);
