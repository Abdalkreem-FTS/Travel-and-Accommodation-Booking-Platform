namespace HotelBooking.Application.Deals.Dtos;

public sealed record UpdateDealRequest(
    int DiscountPercentage,
    DateOnly StartsOn,
    DateOnly EndsOn,
    bool IsFeatured);
