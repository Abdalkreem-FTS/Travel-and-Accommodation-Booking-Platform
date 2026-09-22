using Microsoft.Extensions.Diagnostics.HealthChecks;

using StackExchange.Redis;

namespace HotelBooking.Infrastructure.Diagnostics;

internal sealed class CacheHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext healthCheckContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await redis.GetDatabase().PingAsync();

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Degraded(
                "Redis is unreachable: token revocation is unenforced on reads and rate limits are "
                + "not applied.",
                exception);
        }
    }
}
