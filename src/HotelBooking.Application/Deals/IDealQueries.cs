using HotelBooking.Application.Common;
using HotelBooking.Application.Deals.Dtos;

namespace HotelBooking.Application.Deals;

public interface IDealQueries
{
    Task<IReadOnlyList<FeaturedDealRow>> ListFeaturedAsync(
        int limit,
        DateOnly onDate,
        CancellationToken cancellationToken = default);

    Task<PagedList<DealDto>> ListForRoomAsync(
        Guid roomId,
        PageRequest paging,
        CancellationToken cancellationToken = default);
}
