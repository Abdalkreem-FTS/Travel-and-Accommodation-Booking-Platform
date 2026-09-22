using HotelBooking.Domain.Rooms;

namespace HotelBooking.Application.Deals.Dtos;

public sealed record FeaturedDealRow(
    Guid Id,
    Guid HotelId,
    string HotelName,
    Guid CityId,
    string CityName,
    string? ThumbnailUrl,
    int StarRating,
    Guid RoomId,
    RoomType RoomType,
    decimal OriginalPrice,
    string Currency,
    int DiscountPercentage,
    DateOnly EndsOn);
