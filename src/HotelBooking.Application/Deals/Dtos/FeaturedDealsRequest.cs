namespace HotelBooking.Application.Deals.Dtos;

public sealed record FeaturedDealsRequest(bool? Featured = null, int? Limit = null);
