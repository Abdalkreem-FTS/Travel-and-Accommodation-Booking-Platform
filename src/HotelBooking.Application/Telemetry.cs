using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HotelBooking.Application;

public static class Telemetry
{
    public const string MeterName = "HotelBooking";

    public const string ActivitySourceName = "HotelBooking";

    private static readonly Meter Meter = new(MeterName);

    public static readonly ActivitySource Source = new(ActivitySourceName);

    public static readonly Counter<long> LoginsSucceeded =
        Meter.CreateCounter<long>("auth.login.succeeded", description: "Successful logins.");

    public static readonly Counter<long> LoginsFailed =
        Meter.CreateCounter<long>("auth.login.failed", description: "Failed login attempts.");

    public static readonly Counter<long> UsersRegistered =
        Meter.CreateCounter<long>("users.registered", description: "Successful registrations.");

    public static readonly Counter<long> RefreshTokensReused =
        Meter.CreateCounter<long>("auth.refresh.reused", description: "Replayed refresh tokens detected.");

    public static readonly Counter<long> SessionsEnded =
        Meter.CreateCounter<long>("auth.sessions.ended", description: "Sessions ended by an explicit logout.");

    public static readonly Counter<long> UserRolesGranted =
        Meter.CreateCounter<long>("users.roles.granted", description: "Roles granted to a user by an administrator.");

    public static readonly Counter<long> UserRolesRevoked =
        Meter.CreateCounter<long>("users.roles.revoked", description: "Roles revoked from a user by an administrator.");

    public static readonly Counter<long> BookingsCreated =
        Meter.CreateCounter<long>("bookings.created", description: "Bookings reserved at checkout, waiting for payment.");

    public static readonly Counter<long> BookingConflicts =
        Meter.CreateCounter<long>(
            "bookings.conflicts", description: "Checkouts refused because the nights were already sold.");

    public static readonly Counter<long> IdempotentRequestsInProgress =
        Meter.CreateCounter<long>(
            "idempotency.in_progress", description: "Checkouts refused as a duplicate of one in flight.");

    public static readonly Counter<long> IdempotentReplays =
        Meter.CreateCounter<long>(
            "idempotency.replays",
            description: "Checkouts answered with the booking an earlier submission of the same key made.");

    public static readonly Counter<long> BookingsCancelled =
        Meter.CreateCounter<long>(
            "bookings.cancelled",
            description: "Bookings cancelled by the guest, releasing their room-nights.");

    public static readonly Counter<long> PaymentsStarted =
        Meter.CreateCounter<long>(
            "payments.started",
            description: "Checkouts handed to the guest: nights held, a hosted payment page open.");

    public static readonly Counter<long> PaymentsExpired =
        Meter.CreateCounter<long>(
            "payments.expired",
            description: "Payments that ended without money, releasing their nights, tagged by reason.");

    public static readonly Counter<long> PaymentsSucceeded =
        Meter.CreateCounter<long>(
            "payments.succeeded", description: "Payments the provider confirmed, confirming their booking.");

    public static readonly Histogram<double> PaymentTimeToPay =
        Meter.CreateHistogram<double>(
            "payments.time_to_pay",
            unit: "s",
            description: "From the checkout being handed to the guest to the provider confirming the money.");

    public static readonly Counter<long> PaymentEvents =
        Meter.CreateCounter<long>(
            "payments.events",
            description:
            "Provider events received, tagged by type and outcome. An `unreconciled` outcome is money "
            + "taken that no booking holds: it needs a refund.");

    public static readonly Counter<long> Refunds =
        Meter.CreateCounter<long>(
            "payments.refunds",
            description:
            "Refunds, tagged by status: requested, succeeded, failed. A failed refund is money a guest "
            + "is owed that the provider would not return.");

    public static readonly Counter<long> HotelSearches =
        Meter.CreateCounter<long>(
            "hotels.searches", description: "Searches served by the hotels collection endpoint.");

    public static readonly Counter<long> RoomAvailabilityQueries =
        Meter.CreateCounter<long>(
            "rooms.availability.queries",
            description: "Reads of a hotel's rooms sub-resource, tagged by whether a stay was given.");

    public static readonly Counter<long> CatalogDeletesRefused =
        Meter.CreateCounter<long>(
            "catalog.deletes.refused",
            description: "Catalog deletes refused because something still references the record.");

    public static readonly Counter<long> CacheHits =
        Meter.CreateCounter<long>(
            "cache.hits",
            description: "Reads served from the cache, tagged by key prefix.");

    public static readonly Counter<long> CacheMisses =
        Meter.CreateCounter<long>(
            "cache.misses",
            description: "Reads the cache could not serve, tagged by key prefix.");

    public static readonly Counter<long> CacheUnavailable =
        Meter.CreateCounter<long>(
            "cache.unavailable",
            description: "Cache operations that could not reach Redis, tagged by key prefix.");

    public static readonly Counter<long> DenylistUnavailable =
        Meter.CreateCounter<long>(
            "auth.denylist.unavailable", description: "Denylist operations that could not reach Redis.");

    public static readonly Counter<long> HotelViewsRecorded =
        Meter.CreateCounter<long>(
            "hotels.views.recorded",
            description: "Hotel detail reads counted towards the rankings, tagged by whether the "
            + "reader was signed in.");

    public static readonly Counter<long> VisitsUnavailable =
        Meter.CreateCounter<long>(
            "visits.unavailable",
            description: "Visit counter operations that could not reach Redis, tagged by operation. "
            + "Recording failures are lost views; read failures are a home page served without a "
            + "strip.");

    public static readonly Counter<long> DealsUnpriceable =
        Meter.CreateCounter<long>(
            "deals.unpriceable",
            description: "Featured deals left off the home page because the discount they hold is "
            + "not one the domain accepts. Any value above zero is bad catalogue data.");

    public static readonly Counter<long> DealOverlapsRefused =
        Meter.CreateCounter<long>(
            "deals.overlaps.refused",
            description: "Deal writes refused because another deal already discounts one of those "
            + "room-nights.");

    public static readonly Counter<long> CartItemsAdded =
        Meter.CreateCounter<long>("cart.items.added", description: "Stays added to a cart.");

    public static readonly Counter<long> CartUnavailable =
        Meter.CreateCounter<long>(
            "cart.unavailable",
            description: "Cart operations that could not reach Redis, tagged by operation.");

    public static readonly Counter<long> OutboxPublished =
        Meter.CreateCounter<long>(
            "outbox.published", description: "Outbox messages the dispatcher handed to their handler.");

    public static readonly Counter<long> OutboxFailed =
        Meter.CreateCounter<long>(
            "outbox.failed", description: "Outbox publish attempts that threw and will be retried.");

    public static readonly Counter<long> OutboxAbandoned =
        Meter.CreateCounter<long>(
            "outbox.abandoned",
            description:
            "Outbox messages that spent every attempt and will never be published. Any value "
            + "above zero is a side effect the system promised and did not deliver.");

    public static readonly Counter<long> EmailsSent =
        Meter.CreateCounter<long>("email.sent", description: "Messages the mail server accepted.");

    public static readonly Counter<long> EmailsFailed =
        Meter.CreateCounter<long>(
            "email.failed",
            description:
            "Messages the mail server refused or never answered for. These are retried by the "
            + "outbox lease, so a rising count without a rising outbox.abandoned is SMTP being slow.");

    public static readonly Histogram<double> BookingDuration =
        Meter.CreateHistogram<double>(
            "bookings.duration",
            unit: "ms",
            description:
            "How long a checkout took end to end, tagged by outcome. This is the money path: a "
            + "guest abandons a slow checkout, and the conflict retries a contended one makes it "
            + "slower still.");

    public static readonly Histogram<double> SearchDuration =
        Meter.CreateHistogram<double>(
            "search.duration",
            unit: "ms",
            description: "How long a hotel search took, the heaviest read path in the API.");

    public static readonly Histogram<double> OutboxDispatchDuration =
        Meter.CreateHistogram<double>(
            "outbox.dispatch.duration",
            unit: "ms",
            description: "How long one claim-and-publish cycle took.");

    private static long _outboxPending;

    public static readonly ObservableGauge<long> OutboxPending =
        Meter.CreateObservableGauge(
            "outbox.pending",
            () => Interlocked.Read(ref _outboxPending),
            description: "Committed outbox messages still waiting to be published.");

    public static void ReportOutboxPending(long pending) => Interlocked.Exchange(ref _outboxPending, pending);
}
