using HotelBooking.Application.Abstractions;

using StackExchange.Redis;

namespace HotelBooking.Infrastructure.Authentication;

public sealed class RedisTokenDenylist(IConnectionMultiplexer redis) : ITokenDenylist
{
    private const string KeyPrefix = "auth:revoked-session:";

    public async Task DenySessionAsync(
        Guid sessionId,
        TimeSpan revocationWindow,
        CancellationToken cancellationToken = default)
    {
        if (revocationWindow <= TimeSpan.Zero)
        {
            return;
        }

        await redis.GetDatabase().StringSetAsync(Key(sessionId), string.Empty, revocationWindow);
    }

    public async Task<bool> IsSessionDeniedAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await redis.GetDatabase().KeyExistsAsync(Key(sessionId));
    }

    private static RedisKey Key(Guid sessionId) => KeyPrefix + sessionId.ToString("N");
}
