namespace HotelBooking.Infrastructure.Outbox;

public interface IOutboxMessageHandler
{
    string MessageType { get; }

    Task HandleAsync(OutboxEnvelope message, CancellationToken cancellationToken);
}
