using System.Text.Json;

using HotelBooking.Domain.Abstractions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HotelBooking.Infrastructure.Outbox;

internal sealed class DrainDomainEventsInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            Drain(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result) =>
        throw new NotSupportedException(
            "Save through SaveChangesAsync. The synchronous path cannot drain domain events into "
            + "the outbox, so using it would discard them.");

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
        {
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        foreach (var root in Roots(eventData.Context))
        {
            root.ClearDomainEvents();
        }

        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private static void Drain(DbContext context)
    {
        var raised = Roots(context).SelectMany(root => root.DomainEvents).ToList();

        foreach (var domainEvent in raised)
        {
            context.Set<OutboxMessage>().Add(OutboxMessage.From(domainEvent, SerializerOptions));
        }
    }

    private static List<IHasDomainEvents> Roots(DbContext context) =>
        [.. context.ChangeTracker.Entries<IHasDomainEvents>().Select(entry => entry.Entity)];
}
