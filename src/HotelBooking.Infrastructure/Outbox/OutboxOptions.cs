namespace HotelBooking.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; init; } = 20;

    public TimeSpan ClaimLease { get; init; } = TimeSpan.FromMinutes(5);

    public int MaxAttempts { get; init; } = 5;
}
