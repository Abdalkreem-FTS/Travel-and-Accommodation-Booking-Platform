using HotelBooking.Domain.Abstractions;

namespace HotelBooking.Domain.Bookings.Events;

public sealed record BookingCancelled(
    Guid EventId,
    Guid BookingId,
    Guid UserId,
    Guid HotelId,
    string ConfirmationNumber,
    CancellationReason Reason,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
