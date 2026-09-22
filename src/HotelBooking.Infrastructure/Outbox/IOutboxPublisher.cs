namespace HotelBooking.Infrastructure.Outbox;

public sealed record OutboxEnvelope(Guid Id, string Type, string Content);

public interface IOutboxPublisher
{
    Task PublishAsync(OutboxEnvelope message, CancellationToken cancellationToken = default);
}
