namespace HotelBooking.Application.Bookings.Dtos;

public sealed record BookingSummaryDto(
    Guid Id,
    Guid HotelId,
    string HotelName,
    string ConfirmationNumber,
    string Status,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Rooms,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAtUtc);
