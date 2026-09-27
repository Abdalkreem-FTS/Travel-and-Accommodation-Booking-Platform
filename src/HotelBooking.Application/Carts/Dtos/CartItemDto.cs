using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Carts;
using HotelBooking.Domain.Deals;

namespace HotelBooking.Application.Carts.Dtos;

public sealed record CartItemDto(
    string Id,
    Guid RoomId,
    Guid HotelId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int Adults,
    int Children,
    decimal NightlyRate,
    decimal Total,
    decimal Discount,
    string Currency)
{
    public static CartItemDto From(CartItem item, IReadOnlyList<Deal> deals)
    {
        var priced = BookingPricingService.PriceStay(
            item.NightlyRate, item.RoomId, item.Stay, deals);

        var total = priced.IsSuccess ? priced.Value.Total.Amount : item.Total.Amount;
        var discount = priced.IsSuccess ? priced.Value.Discount.Amount : 0m;

        return new CartItemDto(
            item.Id,
            item.RoomId,
            item.HotelId,
            item.Stay.CheckIn,
            item.Stay.CheckOut,
            item.Stay.Nights,
            item.Guests.Adults,
            item.Guests.Children,
            item.NightlyRate.Amount,
            total,
            discount,
            item.Total.Currency);
    }
}
