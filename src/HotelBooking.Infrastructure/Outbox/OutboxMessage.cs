using System.Diagnostics;
using System.Text.Json;

using HotelBooking.Domain.Abstractions;

namespace HotelBooking.Infrastructure.Outbox;

internal sealed class OutboxMessage
{
    public const int MaxTypeLength = 200;

    /// <summary>Traceparent is exactly "00-{32 hex}-{16 hex}-{2 hex}"</summary>
    public const int TraceParentLength = 55;

    private OutboxMessage(
        Guid id, string type, string content, DateTimeOffset occurredOnUtc, string? traceParent)
    {
        Id = id;
        Type = type;
        Content = content;
        OccurredOnUtc = occurredOnUtc;
        TraceParent = traceParent;
    }

    private OutboxMessage()
    {
        Type = null!;
        Content = null!;
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; }

    public string Content { get; private set; }

    public DateTimeOffset OccurredOnUtc { get; private set; }

    public string? TraceParent { get; private set; }

    public DateTimeOffset? ProcessingAtUtc { get; private set; }

    public DateTimeOffset? ProcessedOnUtc { get; private set; }

    public int Attempts { get; private set; }

    public string? Error { get; private set; }

    public static OutboxMessage From(IDomainEvent domainEvent, JsonSerializerOptions options) =>
        new(domainEvent.EventId,
            domainEvent.GetType().Name,
            JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), options),
            domainEvent.OccurredAtUtc,
            Activity.Current?.Id);
}
