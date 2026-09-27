using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Visits;

public interface IVisitRankings
{
    Task<Result<RankedCities>> TrendingCityIdsAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<Guid>>> RecentlyViewedHotelIdsAsync(
        Guid userId,
        int count,
        CancellationToken cancellationToken = default);
}

public sealed record RankedCities(IReadOnlyList<Guid> CityIds, int TotalCount);
