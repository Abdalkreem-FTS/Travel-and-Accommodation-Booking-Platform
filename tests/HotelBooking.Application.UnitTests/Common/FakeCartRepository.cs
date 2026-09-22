using HotelBooking.Domain.Carts;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.UnitTests.Common;

internal sealed class FakeCartRepository : ICartRepository
{
    private readonly Dictionary<Guid, Dictionary<string, CartItem>> _carts = [];

    public bool IsUnreachable { get; set; }

    public Task<Result<Cart>> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return IsUnreachable ? Task.FromResult<Result<Cart>>(CartErrors.Unavailable) : Task.FromResult<Result<Cart>>(Cart.Restore(userId, Held(userId)));
    }

    public Task<Result<Success>> SaveItemAsync(
        Guid userId,
        CartItem item,
        CancellationToken cancellationToken = default)
    {
        if (IsUnreachable)
        {
            return Task.FromResult<Result<Success>>(CartErrors.Unavailable);
        }

        Items(userId)[item.Id] = item;

        return Task.FromResult<Result<Success>>(Result.Success);
    }

    public Task<Result<Success>> RemoveItemsAsync(
        Guid userId,
        IReadOnlyCollection<string> itemIds,
        CancellationToken cancellationToken = default)
    {
        if (IsUnreachable)
        {
            return Task.FromResult<Result<Success>>(CartErrors.Unavailable);
        }

        foreach (var itemId in itemIds)
        {
            Items(userId).Remove(itemId);
        }

        return Task.FromResult<Result<Success>>(Result.Success);
    }

    public Task<Result<Deleted>> ClearAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (IsUnreachable)
        {
            return Task.FromResult<Result<Deleted>>(CartErrors.Unavailable);
        }

        _carts.Remove(userId);

        return Task.FromResult<Result<Deleted>>(Result.Deleted);
    }

    public IReadOnlyCollection<CartItem> Held(Guid userId) =>
        _carts.TryGetValue(userId, out var held) ? held.Values : [];

    public void Seed(Guid userId, params CartItem[] items)
    {
        foreach (var item in items)
        {
            Items(userId)[item.Id] = item;
        }
    }

    private Dictionary<string, CartItem> Items(Guid userId)
    {
        if (_carts.TryGetValue(userId, out var held))
        {
            return held;
        }

        held = [];
        _carts[userId] = held;

        return held;
    }
}
