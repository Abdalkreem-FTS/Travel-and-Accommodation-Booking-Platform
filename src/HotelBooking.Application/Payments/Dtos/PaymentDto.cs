using HotelBooking.Domain.Payments;

namespace HotelBooking.Application.Payments.Dtos;

public sealed record PaymentDto(
    Guid Id,
    string Status,
    decimal Amount,
    string Currency,
    string? CheckoutUrl,
    DateTimeOffset ExpiresAtUtc,
    RefundDto? Refund)
{
    public static PaymentDto From(Payment payment) => new(
        payment.Id,
        payment.Status.ToString(),
        payment.Amount.Amount,
        payment.Amount.Currency,
        payment.CheckoutUrl,
        payment.ExpiresAtUtc,
        RefundDto.From(payment));
}
