using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HotelBooking.Infrastructure.Outbox;

internal sealed class InProcessOutboxPublisher(
    IServiceScopeFactory scopeFactory,
    ILogger<InProcessOutboxPublisher> logger) : IOutboxPublisher
{
    public async Task PublishAsync(OutboxEnvelope message, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();

        var handlers = scope.ServiceProvider
            .GetServices<IOutboxMessageHandler>()
            .Where(handler => handler.MessageType == message.Type)
            .ToList();

        if (handlers.Count == 0)
        {
            throw new InvalidOperationException(
                $"No handler is registered for outbox message type '{message.Type}'.");
        }

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(message, cancellationToken);

            logger.LogDebug(
                "Handled outbox message {MessageId} of type {MessageType} with {Handler}",
                message.Id, message.Type, handler.GetType().Name);
        }
    }
}
