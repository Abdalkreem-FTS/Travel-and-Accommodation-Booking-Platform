using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Carts;

public sealed class Cart : AggregateRoot<Guid>
{
    public const int MaxItems = 10;

    private readonly List<CartItem> _items;

    private Cart(Guid userId, List<CartItem> items) : base(userId) => _items = items;

    public Guid UserId => Id;

    public IReadOnlyList<CartItem> Items => _items;

    public bool IsEmpty => _items.Count == 0;

    public string? Currency => _items.Count == 0 ? null : _items[0].Total.Currency;

    public decimal TotalAmount => _items.Sum(item => item.Total.Amount);

    public static Cart For(Guid userId) => new(userId, []);

    public static Cart Restore(Guid userId, IEnumerable<CartItem> items) => new(userId, [.. items]);

    public Result<Success> Add(CartItem item)
    {
        if (Currency is { } currency && item.Total.Currency != currency)
        {
            return CartErrors.CurrencyMismatch;
        }

        var existing = _items.FindIndex(held => held.Id == item.Id);

        if (existing >= 0)
        {
            _items[existing] = item;

            return Result.Success;
        }

        if (_items.Count >= MaxItems)
        {
            return CartErrors.Full;
        }

        _items.Add(item);

        return Result.Success;
    }

    public Result<Deleted> Remove(string itemId) =>
        _items.RemoveAll(item => item.Id == itemId) > 0
            ? Result.Deleted
            : CartErrors.ItemNotFound;

    public IReadOnlyList<string> Prune(DateOnly today)
    {
        List<string> dropped = [.. _items.Where(item => !item.IsStillBookable(today)).Select(item => item.Id)];

        _items.RemoveAll(item => dropped.Contains(item.Id));

        return dropped;
    }
}
