using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using HotelBooking.Application.Payments;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Payments;
using HotelBooking.Domain.Results;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelBooking.Infrastructure.Payments;

internal sealed class FakePaymentProvider(
    IOptions<FakePaymentOptions> options,
    ILogger<FakePaymentProvider> logger) : IPaymentProvider
{
    public Task<Result<ProviderCheckout>> CreateCheckoutAsync(
        Payment payment,
        CancellationToken cancellationToken = default)
    {
        if (options.Value.FailCheckouts)
        {
            logger.LogWarning("Refused to open a checkout for payment {PaymentId}", payment.Id);

            return Task.FromResult<Result<ProviderCheckout>>(PaymentErrors.ProviderUnavailable);
        }

        var id = $"cs_fake_{payment.Id:N}";

        logger.LogInformation(
            "Opened checkout {CheckoutId} for payment {PaymentId}: {Amount} {Currency} due by {ExpiresAtUtc}",
            id, payment.Id, payment.Amount.Amount, payment.Amount.Currency, payment.ExpiresAtUtc);

        return Task.FromResult<Result<ProviderCheckout>>(
            new ProviderCheckout(id, $"https://checkout.fake.invalid/pay/{id}"));
    }

    public Task<Result<CheckoutExpiry>> ExpireCheckoutAsync(
        string providerCheckoutId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Closed checkout {CheckoutId}", providerCheckoutId);

        return Task.FromResult<Result<CheckoutExpiry>>(new CheckoutExpiredUnpaid());
    }

    public Task<Result<ProviderRefund>> RefundAsync(
        Payment payment,
        CancellationToken cancellationToken = default)
    {
        if (options.Value.RejectRefunds)
        {
            logger.LogWarning("Rejected the refund of payment {PaymentId}", payment.Id);

            return Task.FromResult<Result<ProviderRefund>>(PaymentErrors.RefundRejected);
        }

        var id = $"re_fake_{payment.Id:N}";

        logger.LogInformation(
            "Refunded {Amount} {Currency} of payment {PaymentId} as {ProviderRefundId}",
            payment.Amount.Amount, payment.Amount.Currency, payment.Id, id);

        return Task.FromResult<Result<ProviderRefund>>(new ProviderRefund(id, Settled: true));
    }

    public Result<PaymentEvent> ReadEvent(string payload, string? signature)
    {
        if (!IsSigned(payload, signature))
        {
            return PaymentErrors.EventSignatureInvalid;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            var id = root.GetProperty("id").GetString();
            var type = root.GetProperty("type").GetString();

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(type))
            {
                return PaymentErrors.EventMalformed;
            }

            return type switch
            {
                "checkout.completed" => Completed(id, root),
                "checkout.expired" => new CheckoutExpired(id, root.GetProperty("checkoutId").GetString()!),
                _ => new UnhandledPaymentEvent(id, type)
            };
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException
            or InvalidOperationException or FormatException)
        {
            return PaymentErrors.EventMalformed;
        }
    }

    private static Result<PaymentEvent> Completed(string id, JsonElement root)
    {
        var amount = Money.Create(root.GetProperty("amount").GetDecimal(), root.GetProperty("currency").GetString());

        if (amount.IsError)
        {
            return PaymentErrors.EventMalformed;
        }

        return new CheckoutCompleted(
            id,
            root.GetProperty("checkoutId").GetString()!,
            root.GetProperty("paymentId").GetString()!,
            amount.Value);
    }

    private bool IsSigned(string payload, string? signature)
    {
        var secret = options.Value.WebhookSecret;

        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(signature))
        {
            return false;
        }

        var expected = Convert.ToHexString(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)))
            .ToLower(CultureInfo.InvariantCulture);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
    }
}
