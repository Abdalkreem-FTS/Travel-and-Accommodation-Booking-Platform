namespace HotelBooking.Application.Bookings.Dtos;

public sealed record BookingConfirmationDto(
    Guid BookingId,
    string ConfirmationNumber,
    string GuestEmail,
    string GuestName,
    string HotelName,
    string CityName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<BookingConfirmationLineDto> Lines);
