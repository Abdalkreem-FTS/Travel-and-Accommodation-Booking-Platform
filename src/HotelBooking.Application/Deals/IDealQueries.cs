using HotelBooking.Application.Deals.Dtos;

namespace HotelBooking.Application.Deals;

public interface IDealQueries
{
    Task<IReadOnlyList<FeaturedDealRow>> ListFeaturedAsync(
        int limit,
        DateOnly onDate,
        CancellationToken cancellationToken = default);
}
