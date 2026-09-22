using System.Text.Json;

using HotelBooking.Application;
using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Common;
using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace HotelBooking.Infrastructure.Caching;

/// <summary>
/// Cache-aside over Redis
/// </summary>
public sealed class RedisCacheService(
    IConnectionMultiplexer redis,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private const double JitterFraction = 0.1;

    public async Task<TValue?> GetOrSetAsync<TValue>(
        CacheKey key,
        Func<CancellationToken, Task<TValue?>> factory,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
        where TValue : class
    {
        var cached = await ReadAsync<TValue>(key);

        if (cached is not null)
        {
            Telemetry.CacheHits.Add(1, Tag(key));

            return cached;
        }

        Telemetry.CacheMisses.Add(1, Tag(key));

        var value = await factory(cancellationToken);

        if (value is not null)
        {
            await WriteAsync(key, value, timeToLive);
        }

        return value;
    }

    public async Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(key.Value);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ReportUnavailable(key, exception, "remove");
        }
    }

    private async Task<TValue?> ReadAsync<TValue>(CacheKey key)
        where TValue : class
    {
        try
        {
            var cached = await redis.GetDatabase().StringGetAsync(key.Value);

            return cached.IsNullOrEmpty
                ? null
                : JsonSerializer.Deserialize<TValue>((string)cached!, SerializerOptions);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception, "A cached {CachePrefix} entry could not be read back and was ignored", key.Prefix);

            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ReportUnavailable(key, exception, "read");

            return null;
        }
    }

    private async Task WriteAsync<TValue>(CacheKey key, TValue value, TimeSpan timeToLive)
    {
        try
        {
            await redis.GetDatabase().StringSetAsync(
                key.Value, JsonSerializer.Serialize(value, SerializerOptions), Jitter(timeToLive));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ReportUnavailable(key, exception, "write");
        }
    }

    private static TimeSpan Jitter(TimeSpan timeToLive) =>
        timeToLive + (timeToLive * JitterFraction * Random.Shared.NextDouble());

    private void ReportUnavailable(CacheKey key, Exception exception, string operation)
    {
        Telemetry.CacheUnavailable.Add(
            1,
            new KeyValuePair<string, object?>("prefix", key.Prefix),
            new KeyValuePair<string, object?>("operation", operation));

        logger.LogWarning(
            exception,
            "Cache unreachable: the {CacheOperation} of a {CachePrefix} entry was skipped and the "
            + "request was served from SQL Server",
            operation,
            key.Prefix);
    }

    private static KeyValuePair<string, object?> Tag(CacheKey key) => new("prefix", key.Prefix);
}
