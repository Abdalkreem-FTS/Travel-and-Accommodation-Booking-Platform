using HotelBooking.Application.Common;
using HotelBooking.Application.Hotels.Dtos;

namespace HotelBooking.Application.Hotels;

public interface IHotelQueries
{
    Task<PagedList<HotelSummaryDto>> SearchAsync(
        HotelSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HotelSummaryDto>> ListCardsAsync(
        IReadOnlyList<Guid> hotelIds,
        CancellationToken cancellationToken = default);

    Task<PagedList<CityHotelDto>> ListForCityAsync(
        Guid cityId,
        string? search,
        PageRequest paging,
        CancellationToken cancellationToken = default);
}
