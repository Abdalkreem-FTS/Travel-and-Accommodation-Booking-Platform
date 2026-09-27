namespace HotelBooking.Application.Deals.Dtos;

public sealed record CreateDealRequest(
    Guid RoomId,
    int DiscountPercentage,
    DateOnly StartsOn,
    DateOnly EndsOn,
    bool IsFeatured);
