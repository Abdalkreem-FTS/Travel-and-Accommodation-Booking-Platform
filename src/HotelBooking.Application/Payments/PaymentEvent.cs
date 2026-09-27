using HotelBooking.Domain.Common;

namespace HotelBooking.Application.Payments;

public abstract record PaymentEvent(string Id, string Type);

public sealed record CheckoutCompleted(
    string Id,
    string CheckoutId,
    string ProviderPaymentId,
    Money AmountReceived) : PaymentEvent(Id, "checkout.completed");

public sealed record CheckoutExpired(string Id, string CheckoutId) : PaymentEvent(Id, "checkout.expired");

public sealed record RefundSettled(
    string Id,
    Guid PaymentId,
    string ProviderRefundId,
    bool Succeeded) : PaymentEvent(Id, Succeeded ? "refund.succeeded" : "refund.failed");

public sealed record UnhandledPaymentEvent(string Id, string Type) : PaymentEvent(Id, Type);
