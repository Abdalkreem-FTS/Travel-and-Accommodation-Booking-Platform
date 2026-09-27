namespace HotelBooking.Infrastructure.Outbox;

public sealed record OutboxClaim(
    Guid Id, string Type, string Content, int Attempts, string? TraceParent);

public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxClaim>> ClaimAsync(
        int batchSize,
        TimeSpan lease,
        int maxAttempts,
        CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(Guid id, string failure, CancellationToken cancellationToken = default);

    Task<int> CountPendingAsync(int maxAttempts, CancellationToken cancellationToken = default);
}
