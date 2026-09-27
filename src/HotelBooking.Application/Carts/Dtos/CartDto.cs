using HotelBooking.Domain.Carts;
using HotelBooking.Domain.Deals;

namespace HotelBooking.Application.Carts.Dtos;

public sealed record CartDto(
    IReadOnlyList<CartItemDto> Items,
    int ItemCount,
    decimal TotalAmount,
    string? Currency,
    bool Degraded)
{
    public static CartDto Unavailable() => new([], 0, 0m, null, Degraded: true);

    public static CartDto From(Cart cart, IReadOnlyList<Deal> deals)
    {
        List<CartItemDto> items = [.. cart.Items.Select(item => CartItemDto.From(item, deals))];

        return new CartDto(
            items,
            items.Count,
            items.Sum(item => item.Total),
            cart.Currency,
            Degraded: false);
    }
}
