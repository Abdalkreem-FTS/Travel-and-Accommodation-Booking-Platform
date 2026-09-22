using HotelBooking.Application.Common;
using HotelBooking.Domain.Deals;

namespace HotelBooking.Application.Deals.Dtos;

public sealed record DealDto(
    Guid Id,
    Guid HotelId,
    Guid RoomId,
    int DiscountPercentage,
    DateOnly StartsOn,
    DateOnly EndsOn,
    bool IsFeatured,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ModifiedAtUtc,
    string Version)
{
    public static DealDto From(Deal deal, ConcurrencyToken version) => new(
        deal.Id,
        deal.HotelId,
        deal.RoomId,
        deal.Discount.Value,
        deal.StartsOn,
        deal.EndsOn,
        deal.IsFeatured,
        deal.CreatedAtUtc,
        deal.ModifiedAtUtc,
        version.Version);
}
