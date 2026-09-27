using HotelBooking.Domain.Common;

namespace HotelBooking.Application.Payments;

public abstract record CheckoutExpiry;

public sealed record CheckoutExpiredUnpaid : CheckoutExpiry;

public sealed record CheckoutAlreadyPaid(string ProviderPaymentId, Money AmountReceived) : CheckoutExpiry;
