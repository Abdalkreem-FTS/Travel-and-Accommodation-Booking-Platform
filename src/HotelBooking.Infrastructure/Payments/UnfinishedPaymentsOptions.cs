namespace HotelBooking.Infrastructure.Payments;

public sealed class UnfinishedPaymentsOptions
{
    public const string SectionName = "Payments:UnfinishedPayments";

    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(1);

    public int BatchSize { get; init; } = 50;
}
