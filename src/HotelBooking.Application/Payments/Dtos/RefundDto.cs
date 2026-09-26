using HotelBooking.Domain.Payments;

namespace HotelBooking.Application.Payments.Dtos;

public sealed record RefundDto(
    decimal Amount,
    string Currency,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? ResolvedAtUtc)
{
    public static RefundDto? From(Payment payment) => payment.RefundRequestedAtUtc is { } requestedAtUtc
        ? new RefundDto(payment.Amount.Amount, payment.Amount.Currency, requestedAtUtc, payment.RefundResolvedAtUtc)
        : null;
}
