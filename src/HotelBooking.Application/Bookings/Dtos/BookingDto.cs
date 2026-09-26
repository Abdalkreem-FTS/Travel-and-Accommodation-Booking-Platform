using HotelBooking.Application.Payments.Dtos;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Payments;

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
    IReadOnlyList<BookingLineDto> Lines,
    PaymentDto? Payment)
{
    public static BookingDto From(Booking booking, Payment? payment) => new(
        booking.Id,
        booking.HotelId,
        booking.Confirmation.Value,
        booking.Status.ToString(),
        booking.EarliestCheckIn,
        booking.LatestCheckOut,
        booking.TotalPrice.Amount,
        booking.TotalPrice.Currency,
        booking.CreatedAtUtc,
        [.. booking.Lines.Select(BookingLineDto.From)],
        payment is null ? null : PaymentDto.From(payment));
}
