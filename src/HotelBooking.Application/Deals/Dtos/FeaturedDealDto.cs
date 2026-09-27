namespace HotelBooking.Application.Deals.Dtos;

public sealed record FeaturedDealDto(
    Guid Id,
    Guid HotelId,
    string HotelName,
    Guid CityId,
    string CityName,
    string? ThumbnailUrl,
    int StarRating,
    Guid RoomId,
    string RoomType,
    decimal OriginalPrice,
    decimal DiscountedPrice,
    int DiscountPercentage,
    string Currency,
    DateOnly EndsOn);
