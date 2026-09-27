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

    private const string TrendingDayPrefix = "trending:cities:";

    private const string TrendingWindowPrefix = "trending:window:";

    private const string RecentKeyPrefix = "user:";

    private const string RecentKeySuffix = ":recent";

    public async Task RecordHotelViewAsync(
        Guid hotelId,
        Guid cityId,
        Guid? viewerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = redis.GetDatabase();
            var batch = database.CreateBatch();

            var today = TrendingDayKey(Today);

            List<Task> writes =
            [
                batch.SortedSetIncrementAsync(today, Member(cityId), 1),
                batch.KeyExpireAsync(today, DayTimeToLive)
            ];

            if (viewerId is { } viewer)
            {
                var key = RecentKey(viewer);

                writes.Add(batch.SortedSetAddAsync(key, Member(hotelId), Score(dateTimeProvider.UtcNow)));

                writes.Add(batch.SortedSetRemoveRangeByRankAsync(
                    key, 0, -(VisitLimits.RecentHotelsKept + 1)));

                writes.Add(batch.KeyExpireAsync(key, RecentTimeToLive));
            }

            batch.Execute();

            await Task.WhenAll(writes);

            Telemetry.HotelViewsRecorded.Add(
                1, new KeyValuePair<string, object?>("identified", viewerId is not null));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Unreachable(exception, "record");
        }
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
