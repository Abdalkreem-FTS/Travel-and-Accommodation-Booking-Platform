namespace HotelBooking.Infrastructure.Payments;

public sealed class StripePaymentOptions
{
    public const string SectionName = "Payments:Stripe";

    public string SecretKey { get; init; } = string.Empty;

    public string WebhookSecret { get; init; } = string.Empty;

    public string ReturnUrl { get; set; } = "http://localhost:8080/bookings/{BOOKING_ID}";

    public bool IsTestModeKey =>
        SecretKey.StartsWith("sk_test_", StringComparison.Ordinal)
        || SecretKey.StartsWith("rk_test_", StringComparison.Ordinal);
}
