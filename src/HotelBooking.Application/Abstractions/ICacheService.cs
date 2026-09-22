using HotelBooking.Application.Common;

namespace HotelBooking.Application.Abstractions;

public interface ICacheService
{
    Task<TValue?> GetOrSetAsync<TValue>(
        CacheKey key,
        Func<CancellationToken, Task<TValue?>> factory,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
        where TValue : class;

    Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);
}
