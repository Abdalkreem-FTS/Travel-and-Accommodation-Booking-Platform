using HotelBooking.Domain.Bookings;

namespace HotelBooking.Application.Bookings.Dtos;

public sealed record BookingLineDto(
    int LineNumber,
    Guid RoomId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int Adults,
    int Children,
    decimal NightlyRate,
    decimal LineTotal,
    decimal Discount)
{
    public static BookingLineDto From(BookingLine line) => new(
        line.LineNumber,
        line.RoomId,
        line.Stay.CheckIn,
        line.Stay.CheckOut,
        line.Stay.Nights,
        line.Guests.Adults,
        line.Guests.Children,
        line.NightlyRate.Amount,
        line.LineTotal.Amount,
        line.DiscountTotal.Amount);
}
