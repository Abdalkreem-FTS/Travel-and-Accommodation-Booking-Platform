using System.Diagnostics;

using HotelBooking.Application;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelBooking.Infrastructure.Outbox;

public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IOutboxPublisher publisher,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Outbox dispatcher started, draining up to {BatchSize} messages every {PollingInterval}",
            _options.BatchSize, _options.PollingInterval);

        using var timer = new PeriodicTimer(_options.PollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox dispatch cycle failed");
            }

            if (!await WaitForNextTickAsync(timer, stoppingToken))
            {
                break;
            }
        }

        logger.LogInformation("Outbox dispatcher stopped");
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        using var scope = scopeFactory.CreateScope();

        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

        var claimed = await UnsampledAsync(() => store.ClaimAsync(
            _options.BatchSize, _options.ClaimLease, _options.MaxAttempts, cancellationToken));

        foreach (var message in claimed)
        {
            await DispatchAsync(store, message, cancellationToken);
        }

        Telemetry.OutboxDispatchDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

        Telemetry.ReportOutboxPending(
            await UnsampledAsync(() => store.CountPendingAsync(_options.MaxAttempts, cancellationToken)));

        if (claimed.Count > 0)
        {
            logger.LogInformation("Outbox dispatched {MessageCount} messages", claimed.Count);
        }
    }

    private async Task DispatchAsync(
        IOutboxStore store,
        OutboxClaim message,
        CancellationToken cancellationToken)
    {
        using var activity = StartHandling(message);

        try
        {
            await publisher.PublishAsync(
                new OutboxEnvelope(message.Id, message.Type, message.Content), cancellationToken);

            await store.MarkProcessedAsync(message.Id, cancellationToken);

            Telemetry.OutboxPublished.Add(1);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);

            await RecordFailureAsync(store, message, exception, cancellationToken);
        }
    }

    private static Activity? StartHandling(OutboxClaim message)
    {
        var name = $"handle {message.Type}";

        var activity = ActivityContext.TryParse(message.TraceParent, null, out var causedBy)
            ? Telemetry.Source.StartActivity(name, ActivityKind.Consumer, causedBy)
            : Telemetry.Source.StartActivity(name, ActivityKind.Consumer);

        activity?.SetTag("messaging.system", "outbox");
        activity?.SetTag("messaging.operation.name", "handle");
        activity?.SetTag("messaging.message.id", message.Id);
        activity?.SetTag("messaging.destination.name", message.Type);

        return activity;
    }

    private static async Task<T> UnsampledAsync<T>(Func<Task<T>> bookkeeping)
    {
        using var untraced = new Activity("outbox bookkeeping");

        untraced.SetIdFormat(ActivityIdFormat.W3C);

        untraced.SetParentId(
            ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);

        untraced.Start();

        return await bookkeeping();
    }

    private async Task RecordFailureAsync(
        IOutboxStore store,
        OutboxClaim message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        await store.MarkFailedAsync(
            message.Id, $"{exception.GetType().Name}: {exception.Message}", cancellationToken);

        Telemetry.OutboxFailed.Add(1);

        if (message.Attempts < _options.MaxAttempts)
        {
            logger.LogWarning(
                exception,
                "Outbox message {MessageId} of type {MessageType} failed on attempt {Attempt} of "
                + "{MaxAttempts}, retrying after its lease expires",
                message.Id, message.Type, message.Attempts, _options.MaxAttempts);

            return;
        }

        Telemetry.OutboxAbandoned.Add(1);

        logger.LogError(
            exception,
            "Outbox message {MessageId} of type {MessageType} abandoned after {Attempts} attempts "
            + "and will not be retried. Whatever it was going to do has not happened",
            message.Id, message.Type, message.Attempts);
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
