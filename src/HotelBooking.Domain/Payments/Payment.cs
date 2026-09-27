using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Payments;

public sealed class Payment : AggregateRoot<Guid>
{
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(30);

    private Payment(Guid id, Booking booking, DateTimeOffset nowUtc)
        : base(id)
    {
        BookingId = booking.Id;
        Amount = booking.TotalPrice;
        Status = PaymentStatus.Pending;
        ExpiresAtUtc = nowUtc + HoldDuration;
    }

    private Payment()
    {
        Amount = null!;
    }

    public Guid BookingId { get; private set; }

    public Money Amount { get; private set; }

    public PaymentStatus Status { get; private set; }

    public string? ProviderCheckoutId { get; private set; }

    public string? CheckoutUrl { get; private set; }

    public string? ProviderPaymentId { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public string? ProviderRefundId { get; private set; }

    public DateTimeOffset? RefundRequestedAtUtc { get; private set; }

    public DateTimeOffset? RefundResolvedAtUtc { get; private set; }

    public static Result<Payment> Start(Guid id, Booking booking, DateTimeOffset nowUtc) =>
        booking.Status is BookingStatus.Pending
            ? new Payment(id, booking, nowUtc)
            : PaymentErrors.BookingNotPending;

    public Result<Updated> AttachCheckout(string providerCheckoutId, string checkoutUrl)
    {
        if (Status is not PaymentStatus.Pending)
        {
            return PaymentErrors.InvalidTransition;
        }

        if (ProviderCheckoutId is not null)
        {
            return PaymentErrors.CheckoutAlreadyAttached;
        }

        ProviderCheckoutId = providerCheckoutId;
        CheckoutUrl = checkoutUrl;

        return Result.Updated;
    }

    public Result<Updated> MarkSucceeded(
        string providerPaymentId,
        Money amountReceived,
        DateTimeOffset nowUtc)
    {
        if (Status is not PaymentStatus.Pending)
        {
            return PaymentErrors.InvalidTransition;
        }

        if (amountReceived != Amount)
        {
            return PaymentErrors.AmountMismatch;
        }

        Status = PaymentStatus.Succeeded;
        ProviderPaymentId = providerPaymentId;
        ResolvedAtUtc = nowUtc;

        return Result.Updated;
    }

    public Result<Updated> AcceptLatePayment(
        string providerPaymentId,
        Money amountReceived,
        DateTimeOffset nowUtc)
    {
        if (Status is not PaymentStatus.Expired)
        {
            return PaymentErrors.InvalidTransition;
        }

        if (amountReceived != Amount)
        {
            return PaymentErrors.AmountMismatch;
        }

        Status = PaymentStatus.Succeeded;
        ProviderPaymentId = providerPaymentId;
        ResolvedAtUtc = nowUtc;

        return Result.Updated;
    }

    public Result<Updated> Expire(DateTimeOffset nowUtc)
    {
        if (Status is not PaymentStatus.Pending)
        {
            return PaymentErrors.InvalidTransition;
        }

        Status = PaymentStatus.Expired;
        ResolvedAtUtc = nowUtc;

        return Result.Updated;
    }

    public Result<Updated> RefundInFull(DateTimeOffset nowUtc)
    {
        if (Status is not PaymentStatus.Succeeded)
        {
            return PaymentErrors.InvalidTransition;
        }

        Status = PaymentStatus.Refunding;
        RefundRequestedAtUtc = nowUtc;

        return Result.Updated;
    }

    public Result<Updated> RecordRefundSent(string providerRefundId)
    {
        if (Status is not PaymentStatus.Refunding)
        {
            return PaymentErrors.InvalidTransition;
        }

        ProviderRefundId = providerRefundId;

        return Result.Updated;
    }

    public Result<Updated> CompleteRefund(string providerRefundId, DateTimeOffset nowUtc)
    {
        if (Status is not PaymentStatus.Refunding)
        {
            return PaymentErrors.InvalidTransition;
        }

        Status = PaymentStatus.Refunded;
        ProviderRefundId = providerRefundId;
        RefundResolvedAtUtc = nowUtc;

        return Result.Updated;
    }

    public Result<Updated> FailRefund(DateTimeOffset nowUtc)
    {
        if (Status is not PaymentStatus.Refunding)
        {
            return PaymentErrors.InvalidTransition;
        }

        Status = PaymentStatus.RefundFailed;
        RefundResolvedAtUtc = nowUtc;

        return Result.Updated;
    }
}
