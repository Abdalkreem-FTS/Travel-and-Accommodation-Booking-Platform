namespace HotelBooking.Infrastructure.Payments;

public sealed class FakePaymentOptions
{
    public const string SectionName = "Payments:Fake";

    public bool DeclineAuthorizations { get; set; }

    public bool FailCaptures { get; set; }
}
