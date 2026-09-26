namespace HotelBooking.Infrastructure.Payments;

public sealed class FakePaymentOptions
{
    public const string SectionName = "Payments:Fake";

    public bool FailCheckouts { get; set; }

    public bool RejectRefunds { get; set; }

    public string WebhookSecret { get; set; } = string.Empty;
}
