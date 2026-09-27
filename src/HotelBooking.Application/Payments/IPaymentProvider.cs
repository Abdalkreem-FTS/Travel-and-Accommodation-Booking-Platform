using HotelBooking.Domain.Payments;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Payments;

public interface IPaymentProvider
{
    Task<Result<ProviderCheckout>> CreateCheckoutAsync(
        Payment payment,
        CancellationToken cancellationToken = default);

    Task<Result<CheckoutExpiry>> ExpireCheckoutAsync(
        string providerCheckoutId,
        CancellationToken cancellationToken = default);

    Task<Result<ProviderRefund>> RefundAsync(
        Payment payment,
        CancellationToken cancellationToken = default);

    Result<PaymentEvent> ReadEvent(string payload, string? signature);
}
