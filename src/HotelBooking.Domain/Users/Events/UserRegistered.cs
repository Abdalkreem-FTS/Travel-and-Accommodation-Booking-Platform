using HotelBooking.Domain.Abstractions;

namespace HotelBooking.Domain.Users.Events;

public sealed record UserRegistered(
    Guid EventId,
    Guid UserId,
    string Email,
    string FirstName,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
