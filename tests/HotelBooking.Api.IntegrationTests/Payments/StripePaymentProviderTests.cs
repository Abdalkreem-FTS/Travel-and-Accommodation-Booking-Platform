using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using HotelBooking.Application.Payments;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Payments;
using HotelBooking.Domain.Rooms;
using HotelBooking.Infrastructure.Payments;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Stripe;

namespace HotelBooking.Api.IntegrationTests.Payments;

[Trait("Category", "Stripe")]
public sealed class StripePaymentProviderTests
{
    private const string WebhookSecret = "whsec_integration_test_secret";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void ReadEvent_ASignedCompletedCheckout_IsTheMoneyAndTheCheckoutItPaidFor()
    {
        var payload = AnEvent("checkout.session.completed", """
            { "id": "cs_test_1", "object": "checkout.session", "status": "complete", "payment_status": "paid",
              "payment_intent": "pi_test_1", "amount_total": 24100, "currency": "usd" }
            """);

        var read = Provider().ReadEvent(payload, Sign(payload));

        var completed = read.Value.ShouldBeOfType<CheckoutCompleted>();
        completed.CheckoutId.ShouldBe("cs_test_1");
        completed.ProviderPaymentId.ShouldBe("pi_test_1");
        completed.AmountReceived.ShouldBe(Money.Create(241m, "USD").Value, "Stripe counts cents; we count dollars");
    }

    [Fact]
    public void ReadEvent_APayloadChangedAfterSigning_IsRefused()
    {
        var payload = AnEvent("checkout.session.completed", """
            { "id": "cs_test_1", "object": "checkout.session", "status": "complete", "payment_status": "paid",
              "payment_intent": "pi_test_1", "amount_total": 24100, "currency": "usd" }
            """);

        var signature = Sign(payload);
        var tampered = payload.Replace("24100", "100", StringComparison.Ordinal);

        Provider().ReadEvent(tampered, signature).TopError.ShouldBe(PaymentErrors.EventSignatureInvalid);
    }

    [Fact]
    public void ReadEvent_ARefundThatSettled_NamesOurPayment()
    {
        var paymentId = Guid.NewGuid();

        var payload = AnEvent("refund.updated", $$"""
            { "id": "re_test_1", "object": "refund", "status": "succeeded", "amount": 24100, "currency": "usd",
              "metadata": { "payment_id": "{{paymentId}}" } }
            """);

        var settled = Provider().ReadEvent(payload, Sign(payload)).Value.ShouldBeOfType<RefundSettled>();

        settled.PaymentId.ShouldBe(paymentId);
        settled.ProviderRefundId.ShouldBe("re_test_1");
        settled.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Checkout_OpenedThenClosedTwice_EndsClosedBothTimes()
    {
        var provider = Provider(LiveKey());
        var payment = APayment();

        var checkout = await provider.CreateCheckoutAsync(payment, Token);

        checkout.Value.Url.ShouldStartWith("https://checkout.stripe.com/");
        (await provider.ExpireCheckoutAsync(checkout.Value.Id, Token)).Value.ShouldBeOfType<CheckoutExpiredUnpaid>();
        (await provider.ExpireCheckoutAsync(checkout.Value.Id, Token)).Value.ShouldBeOfType<CheckoutExpiredUnpaid>(
            "the unfinished payments worker and a cancellation may both close the same checkout");
    }

    [Fact]
    public async Task Refund_OfAPaidPaymentAskedForTwice_ReturnsTheMoneyOnce()
    {
        var key = LiveKey();
        var provider = Provider(key);

        var intent = await new PaymentIntentService(new StripeClient(key)).CreateAsync(
            new PaymentIntentCreateOptions
            {
                Amount = 24100,
                Currency = "usd",
                PaymentMethod = "pm_card_visa",
                PaymentMethodTypes = ["card"],
                Confirm = true
            },
            cancellationToken: Token);

        var payment = APayment();
        payment.MarkSucceeded(intent.Id, payment.Amount, Now).IsSuccess.ShouldBeTrue();
        payment.RefundInFull(Now).IsSuccess.ShouldBeTrue();

        var first = await provider.RefundAsync(payment, Token);
        var again = await provider.RefundAsync(payment, Token);

        first.Value.Settled.ShouldBeTrue();
        again.Value.Id.ShouldBe(first.Value.Id, "the payment id is Stripe's idempotency key");
    }

    private static StripePaymentProvider Provider(string secretKey = "sk_test_offline") =>
        new(
            new StripeClient(secretKey),
            Options.Create(new StripePaymentOptions { SecretKey = secretKey, WebhookSecret = WebhookSecret }),
            NullLogger<StripePaymentProvider>.Instance);

    private static string LiveKey()
    {
        var key = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");

        Assert.SkipUnless(
            key?.StartsWith("sk_test_", StringComparison.Ordinal) == true,
            "STRIPE_SECRET_KEY does not hold a Stripe test-mode key.");

        return key;
    }

    private static Payment APayment()
    {
        var today = DateOnly.FromDateTime(Now.UtcDateTime);

        var room = Room.Create(
            Guid.NewGuid(), Guid.NewGuid(), "1203", RoomType.Luxury,
            Occupancy.Create(2, 0).Value, Money.Create(120.50m, "USD").Value, Now).Value;

        var booking = Booking.Reserve(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new RoomStay(room, DateRange.Create(today.AddDays(5), today.AddDays(7), today).Value, Occupancy.Create(2, 0).Value)],
            ConfirmationNumber.From(Guid.NewGuid()),
            Now).Value;

        return Payment.Start(Guid.NewGuid(), booking, Now).Value;
    }

    private static string AnEvent(string type, string dataObject) =>
        $$"""
        { "id": "evt_{{Guid.NewGuid():N}}", "object": "event", "api_version": "{{StripeConfiguration.ApiVersion}}",
          "created": {{Now.ToUnixTimeSeconds()}}, "livemode": false, "type": "{{type}}",
          "data": { "object": {{dataObject}} } }
        """;

    private static string Sign(string payload)
    {
        var timestamp = Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(WebhookSecret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));

        return $"t={timestamp},v1={Convert.ToHexString(hash).ToLower(CultureInfo.InvariantCulture)}";
    }
}
