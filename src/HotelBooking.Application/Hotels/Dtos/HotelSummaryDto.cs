namespace HotelBooking.Application.Hotels.Dtos;

public sealed record HotelSummaryDto(
    Guid Id,
    string Name,
    Guid CityId,
    string CityName,
    int StarRating,
    string? ThumbnailUrl,
    decimal FromPrice,
    string Currency);
