namespace HotelBooking.Infrastructure.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    public const string Stripe = "Stripe";

    public const string Fake = "Fake";

    public string Provider { get; set; } = Fake;
}
