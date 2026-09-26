namespace HotelBooking.Application.Bookings.Dtos;

public sealed record BookingCancellationNoticeDto(
    Guid BookingId,
    string ConfirmationNumber,
    string GuestEmail,
    string GuestName,
    string HotelName,
    string CityName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    decimal? RefundAmount,
    string Currency);
