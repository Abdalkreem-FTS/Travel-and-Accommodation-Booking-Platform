using HotelBooking.Domain.Bookings;

namespace HotelBooking.Application.Bookings.Dtos;

public sealed record BookingDto(
    Guid Id,
    Guid HotelId,
    string ConfirmationNumber,
    string Status,
    DateOnly CheckIn,
    DateOnly CheckOut,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<BookingLineDto> Lines)
{
    public static BookingDto From(Booking booking) => new(
        booking.Id,
        booking.HotelId,
        booking.Confirmation.Value,
        booking.Status.ToString(),
        booking.EarliestCheckIn,
        booking.LatestCheckOut,
        booking.TotalPrice.Amount,
        booking.TotalPrice.Currency,
        booking.CreatedAtUtc,
        [.. booking.Lines.Select(BookingLineDto.From)]);
}
