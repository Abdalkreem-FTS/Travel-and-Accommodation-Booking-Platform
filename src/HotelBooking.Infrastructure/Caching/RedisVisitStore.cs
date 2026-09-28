using System.Globalization;

using HotelBooking.Application;
using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Visits;
using HotelBooking.Domain.Results;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace HotelBooking.Infrastructure.Caching;

public sealed class RedisVisitStore(
    IConnectionMultiplexer redis,
    IDateTimeProvider dateTimeProvider,
    ILogger<RedisVisitStore> logger) : IVisitTracker, IVisitRankings
{
    public const int TrendingWindowDays = 7;

    private static readonly TimeSpan DayTimeToLive = TimeSpan.FromDays(TrendingWindowDays + 1);

    private static readonly TimeSpan WindowTimeToLive = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan RecentTimeToLive = TimeSpan.FromDays(30);

    private static readonly TimeSpan SeenTimeToLive = TimeSpan.FromDays(1);

    private const string TrendingDayPrefix = "trending:cities:";

    private const string TrendingWindowPrefix = "trending:window:";

    private const string TrendingSeenPrefix = "trending:seen:";

    private const string RecentKeyPrefix = "user:";

    private const string RecentKeySuffix = ":recent";

    public async Task RecordHotelViewAsync(
        Guid hotelId,
        Guid cityId,
        Guid? viewerId,
        string? clientAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = redis.GetDatabase();

            var counted = CountVisitAsync(database, cityId, Visitor(viewerId, clientAddress));

            List<Task> writes = [counted];

            if (viewerId is { } viewer)
            {
                var batch = database.CreateBatch();
                var key = RecentKey(viewer);

                writes.Add(batch.SortedSetAddAsync(key, Member(hotelId), Score(dateTimeProvider.UtcNow)));

                writes.Add(batch.SortedSetRemoveRangeByRankAsync(
                    key, 0, -(VisitLimits.RecentHotelsKept + 1)));

                writes.Add(batch.KeyExpireAsync(key, RecentTimeToLive));

                batch.Execute();
            }

            await Task.WhenAll(writes);

            Telemetry.HotelViewsRecorded.Add(
                1, new KeyValuePair<string, object?>("identified", viewerId is not null));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Unreachable(exception, "record");
        }
    }

    private Task<bool> CountVisitAsync(IDatabase database, Guid cityId, string? visitor)
    {
        if (visitor is null)
        {
            return Task.FromResult(false);
        }

        var today = Today;
        var day = TrendingDayKey(today);
        var seen = SeenKey(today, cityId, visitor);

        var transaction = database.CreateTransaction();
        transaction.AddCondition(Condition.KeyNotExists(seen));

        _ = transaction.StringSetAsync(seen, 1, SeenTimeToLive);
        _ = transaction.SortedSetIncrementAsync(day, Member(cityId), 1);
        _ = transaction.KeyExpireAsync(day, DayTimeToLive);

        return transaction.ExecuteAsync();
    }

    public async Task ForgetCityAsync(Guid cityId, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = redis.GetDatabase();
            var batch = database.CreateBatch();
            var member = Member(cityId);

            List<Task> removals =
            [
                .. WindowDays().Select(day => batch.SortedSetRemoveAsync(TrendingDayKey(day), member)),
                batch.SortedSetRemoveAsync(TrendingWindowKey(Today), member)
            ];

            batch.Execute();

            await Task.WhenAll(removals);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Unreachable(exception, "forget");
        }
    }

    public async Task<Result<RankedCities>> TrendingCityIdsAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = redis.GetDatabase();

            var window = await BuildWindowAsync(database);

            var ranked = await database.SortedSetRangeByRankAsync(
                window, skip, skip + take - 1, Order.Descending);

            var visited = await database.SortedSetLengthAsync(window);

            return new RankedCities(Identifiers(ranked), (int)visited);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unreachable(exception, "trending");
        }
    }

    public async Task<Result<IReadOnlyList<Guid>>> RecentlyViewedHotelIdsAsync(
        Guid userId,
        int count,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var viewed = await redis.GetDatabase().SortedSetRangeByRankAsync(
                RecentKey(userId), 0, count - 1, Order.Descending);

            return Result<IReadOnlyList<Guid>>.From(Identifiers(viewed));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unreachable(exception, "recent");
        }
    }

    private async Task<RedisKey> BuildWindowAsync(IDatabase database)
    {
        var window = TrendingWindowKey(Today);

        if (await database.KeyExistsAsync(window))
        {
            return window;
        }

        await database.SortedSetCombineAndStoreAsync(
            SetOperation.Union, window, [.. WindowDays().Select(TrendingDayKey)]);

        await database.KeyExpireAsync(window, WindowTimeToLive);

        return window;
    }

    private IEnumerable<DateOnly> WindowDays()
    {
        var today = Today;

        return Enumerable.Range(0, TrendingWindowDays).Select(day => today.AddDays(-day));
    }

    private DateOnly Today => DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

    private static RedisKey RecentKey(Guid userId) =>
        RecentKeyPrefix + userId.ToString("N") + RecentKeySuffix;

    public static RedisKey TrendingDayKey(DateOnly day) =>
        TrendingDayPrefix + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    private static RedisKey SeenKey(DateOnly day, Guid cityId, string visitor) =>
        TrendingSeenPrefix + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ":"
        + Member(cityId) + ":" + visitor;

    private static string? Visitor(Guid? viewerId, string? clientAddress) =>
        viewerId is { } viewer ? "user:" + viewer.ToString("N")
        : clientAddress is { Length: > 0 } ? "ip:" + clientAddress
        : null;

    private static RedisKey TrendingWindowKey(DateOnly today) =>
        TrendingWindowPrefix + today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    public static RedisValue Member(Guid id) => id.ToString("N");

    private static double Score(DateTimeOffset moment) =>
        (moment - DateTimeOffset.UnixEpoch).TotalMicroseconds;

    private static IReadOnlyList<Guid> Identifiers(RedisValue[] members) =>
        [.. members
            .Select(member => Guid.TryParseExact(member, "N", out var id) ? id : (Guid?)null)
            .OfType<Guid>()];

    private Error Unreachable(Exception exception, string operation)
    {
        Telemetry.VisitsUnavailable.Add(1, new KeyValuePair<string, object?>("operation", operation));

        logger.LogWarning(
            exception,
            "Redis is unreachable: the {VisitOperation} of the visit counters did not happen",
            operation);

        return VisitErrors.RankingUnavailable;
    }
}
