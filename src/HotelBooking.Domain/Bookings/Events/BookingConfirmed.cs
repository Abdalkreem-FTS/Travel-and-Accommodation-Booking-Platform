using HotelBooking.Domain.Abstractions;

namespace HotelBooking.Domain.Bookings.Events;

public sealed record BookingConfirmed(
    Guid EventId,
    Guid BookingId,
    Guid UserId,
    Guid HotelId,
    string ConfirmationNumber,
    DateOnly CheckIn,
    DateOnly CheckOut,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
