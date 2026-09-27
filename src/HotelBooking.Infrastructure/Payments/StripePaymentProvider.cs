using System.Globalization;

using HotelBooking.Application.Payments;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Payments;
using HotelBooking.Domain.Results;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Stripe;
using Stripe.Checkout;

using PaymentEvent = HotelBooking.Application.Payments.PaymentEvent;

namespace HotelBooking.Infrastructure.Payments;

public sealed class StripePaymentProvider(
    IStripeClient stripe,
    IOptions<StripePaymentOptions> options,
    ILogger<StripePaymentProvider> logger) : IPaymentProvider
{
    private const string PaymentIdKey = "payment_id";
    private const string BookingIdKey = "booking_id";

    private const int MinorUnitsPerMajor = 100;

    private static readonly TimeSpan StripeExpiryMargin = TimeSpan.FromMinutes(2);

    private static readonly HashSet<string> CurrenciesNotInHundredths = new(StringComparer.Ordinal)
    {
        "BHD", "BIF", "CLP", "DJF", "GNF", "JOD", "JPY", "KMF", "KRW", "KWD", "MGA", "OMR", "PYG",
        "RWF", "TND", "UGX", "VND", "VUV", "XAF", "XOF", "XPF"
    };

    private readonly SessionService _sessions = new(stripe);
    private readonly RefundService _refunds = new(stripe);

    public async Task<Result<ProviderCheckout>> CreateCheckoutAsync(
        Payment payment,
        CancellationToken cancellationToken = default)
    {
        if (CurrenciesNotInHundredths.Contains(payment.Amount.Currency))
        {
            logger.LogError(
                "Refused to open a checkout for payment {PaymentId}: {Currency} is not counted in hundredths",
                payment.Id, payment.Amount.Currency);

            return PaymentErrors.ProviderUnavailable;
        }

        var metadata = new Dictionary<string, string>
        {
            [PaymentIdKey] = payment.Id.ToString(),
            [BookingIdKey] = payment.BookingId.ToString()
        };

        var session = await _sessions.CreateAsync(
            new SessionCreateOptions
            {
                Mode = "payment",
                PaymentMethodTypes = ["card"],
                ClientReferenceId = payment.Id.ToString(),
                ExpiresAt = (payment.ExpiresAtUtc + StripeExpiryMargin).UtcDateTime,
                SuccessUrl = options.Value.ReturnUrl.Replace(
                    "{BOOKING_ID}", payment.BookingId.ToString(), StringComparison.Ordinal),
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = payment.Amount.Currency.ToLowerInvariant(),
                            UnitAmount = ToMinorUnits(payment.Amount),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Hotel booking"
                            }
                        }
                    }
                ],
                Metadata = metadata,
                PaymentIntentData = new SessionPaymentIntentDataOptions { Metadata = metadata }
            },
            new RequestOptions { IdempotencyKey = $"checkout-{payment.Id}" },
            cancellationToken);

        logger.LogInformation(
            "Opened Stripe checkout {CheckoutId} for payment {PaymentId}", session.Id, payment.Id);

        return new ProviderCheckout(session.Id, session.Url);
    }

    public async Task<Result<CheckoutExpiry>> ExpireCheckoutAsync(
        string providerCheckoutId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _sessions.ExpireAsync(providerCheckoutId, cancellationToken: cancellationToken);

            return new CheckoutExpiredUnpaid();
        }
        catch (StripeException exception) when (exception.StripeError?.Type == "invalid_request_error") { }

        var session = await _sessions.GetAsync(providerCheckoutId, cancellationToken: cancellationToken);

        return session switch
        {
            { Status: "expired" } => new CheckoutExpiredUnpaid(),
            { Status: "complete", PaymentStatus: "paid" } =>
                new CheckoutAlreadyPaid(session.PaymentIntentId, ToMoney(session.AmountTotal ?? 0, session.Currency)),
            _ => PaymentErrors.ProviderUnavailable
        };
    }

    public async Task<Result<ProviderRefund>> RefundAsync(
        Payment payment,
        CancellationToken cancellationToken = default)
    {
        Stripe.Refund answer;

        if (payment.ProviderRefundId is { } sent)
        {
            answer = await _refunds.GetAsync(sent, cancellationToken: cancellationToken);
        }
        else
        {
            try
            {
                answer = await _refunds.CreateAsync(
                    new RefundCreateOptions
                    {
                        PaymentIntent = payment.ProviderPaymentId,
                        Amount = ToMinorUnits(payment.Amount),
                        Metadata = new Dictionary<string, string> { [PaymentIdKey] = payment.Id.ToString() }
                    },
                    new RequestOptions { IdempotencyKey = $"refund-{payment.Id}" },
                    cancellationToken);
            }
            catch (StripeException exception) when (exception.StripeError?.Type == "invalid_request_error")
            {
                logger.LogError(
                    exception,
                    "Stripe refused the refund of payment {PaymentId} ({StripeCode})",
                    payment.Id, exception.StripeError?.Code);

                return PaymentErrors.RefundRejected;
            }
        }

        return answer.Status switch
        {
            "succeeded" => new ProviderRefund(answer.Id, Settled: true),
            "pending" or "requires_action" => new ProviderRefund(answer.Id, Settled: false),
            _ => PaymentErrors.RefundRejected
        };
    }

    public Result<PaymentEvent> ReadEvent(string payload, string? signature)
    {
        var secret = options.Value.WebhookSecret;

        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(signature))
        {
            return PaymentErrors.EventSignatureInvalid;
        }

        Event stripeEvent;

        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, secret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            return PaymentErrors.EventSignatureInvalid;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "A signed Stripe event could not be read");

            return PaymentErrors.EventMalformed;
        }

        return stripeEvent.Data.Object switch
        {
            Session { Status: "complete", PaymentStatus: "paid" } session
                when stripeEvent.Type == EventTypes.CheckoutSessionCompleted =>
                new CheckoutCompleted(
                    stripeEvent.Id,
                    session.Id,
                    session.PaymentIntentId,
                    ToMoney(session.AmountTotal ?? 0, session.Currency)),

            Session session when stripeEvent.Type == EventTypes.CheckoutSessionExpired =>
                new CheckoutExpired(stripeEvent.Id, session.Id),

            Stripe.Refund stripeRefund
                when stripeEvent.Type is EventTypes.RefundUpdated or EventTypes.RefundFailed =>
                RefundSettledFrom(stripeEvent.Id, stripeEvent.Type, stripeRefund),

            _ => new UnhandledPaymentEvent(stripeEvent.Id, stripeEvent.Type)
        };
    }

    private static PaymentEvent RefundSettledFrom(string eventId, string type, Stripe.Refund stripeRefund)
    {
        var ours = stripeRefund.Metadata is { } metadata
            && metadata.TryGetValue(PaymentIdKey, out var paymentId)
            && Guid.TryParse(paymentId, out _);

        if (!ours)
        {
            return new UnhandledPaymentEvent(eventId, type);
        }

        return stripeRefund.Status switch
        {
            "succeeded" or "failed" or "canceled" => new RefundSettled(
                eventId,
                Guid.Parse(stripeRefund.Metadata![PaymentIdKey]),
                stripeRefund.Id,
                Succeeded: stripeRefund.Status == "succeeded"),
            _ => new UnhandledPaymentEvent(eventId, type)
        };
    }

    private static long ToMinorUnits(Money money) => (long)(money.Amount * MinorUnitsPerMajor);

    private static Money ToMoney(long minorUnits, string currency) =>
        Money.Create(
                decimal.Divide(minorUnits, MinorUnitsPerMajor),
                currency.ToUpper(CultureInfo.InvariantCulture))
            .Value;
}
