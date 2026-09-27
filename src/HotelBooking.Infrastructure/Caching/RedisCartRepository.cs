using System.Text.Json;

using HotelBooking.Application;
using HotelBooking.Domain.Carts;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace HotelBooking.Infrastructure.Caching;

public sealed class RedisCartRepository(
    IConnectionMultiplexer redis,
    ILogger<RedisCartRepository> logger) : ICartRepository
{
    public static readonly TimeSpan TimeToLive = TimeSpan.FromHours(24);

    private const string KeyPrefix = "cart:";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<Cart>> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var fields = await redis.GetDatabase().HashGetAllAsync(Key(userId));

            return Cart.Restore(userId, fields.Select(Read).OfType<CartItem>());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unreachable(userId, exception, "read");
        }
    }

    public async Task<Result<Success>> SaveItemAsync(
        Guid userId,
        CartItem item,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = redis.GetDatabase();

            var batch = database.CreateBatch();

            var written = batch.HashSetAsync(
                Key(userId), item.Id, JsonSerializer.Serialize(StoredCartItem.From(item), SerializerOptions));

            var renewed = batch.KeyExpireAsync(Key(userId), TimeToLive);

            batch.Execute();

            await Task.WhenAll(written, renewed);

            return Result.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unreachable(userId, exception, "write");
        }
    }

    public async Task<Result<Success>> RemoveItemsAsync(
        Guid userId,
        IReadOnlyCollection<string> itemIds,
        CancellationToken cancellationToken = default)
    {
        if (itemIds.Count == 0)
        {
            return Result.Success;
        }

        try
        {
            await redis.GetDatabase().HashDeleteAsync(
                Key(userId), [.. itemIds.Select(itemId => (RedisValue)itemId)]);

            return Result.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unreachable(userId, exception, "remove");
        }
    }

    public async Task<Result<Deleted>> ClearAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(Key(userId));

            return Result.Deleted;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unreachable(userId, exception, "clear");
        }
    }

    public static RedisKey Key(Guid userId) => KeyPrefix + userId.ToString("N");

    private CartItem? Read(HashEntry field)
    {
        try
        {
            var stored = JsonSerializer.Deserialize<StoredCartItem>((string)field.Value!, SerializerOptions);

            return stored?.ToDomain();
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception, "A held stay could not be read back from a cart and was dropped from it");

            return null;
        }
    }

    private Error Unreachable(Guid userId, Exception exception, string operation)
    {
        Telemetry.CartUnavailable.Add(1, new KeyValuePair<string, object?>("operation", operation));

        logger.LogWarning(
            exception,
            "Redis is unreachable: the {CartOperation} of the cart of user {UserId} did not happen",
            operation,
            userId);

        return CartErrors.Unavailable;
    }

    private sealed record StoredCartItem(
        int V,
        Guid RoomId,
        Guid HotelId,
        DateOnly CheckIn,
        DateOnly CheckOut,
        int Adults,
        int Children,
        decimal NightlyRate,
        string Currency)
    {
        private const int Version = 1;

        public static StoredCartItem From(CartItem item) => new(
            Version,
            item.RoomId,
            item.HotelId,
            item.Stay.CheckIn,
            item.Stay.CheckOut,
            item.Guests.Adults,
            item.Guests.Children,
            item.NightlyRate.Amount,
            item.NightlyRate.Currency);

        public CartItem? ToDomain()
        {
            if (V != Version)
            {
                return null;
            }

            var stay = DateRange.Create(CheckIn, CheckOut, CheckIn);
            var guests = Occupancy.Create(Adults, Children);
            var rate = Money.Create(NightlyRate, Currency);

            if (stay.IsError || guests.IsError || rate.IsError)
            {
                return null;
            }

            var item = CartItem.Restore(RoomId, HotelId, stay.Value, guests.Value, rate.Value);

            return item.IsSuccess ? item.Value : null;
        }
    }
}
