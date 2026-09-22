using HotelBooking.Application.Cities.Dtos;
using HotelBooking.Application.Common;

namespace HotelBooking.Application.Cities;

public interface ICityQueries
{
    Task<PagedList<CitySummaryDto>> ListAsync(
        CityListCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CitySummaryDto>> ListByIdsAsync(
        IReadOnlyList<Guid> cityIds,
        CancellationToken cancellationToken = default);
}
