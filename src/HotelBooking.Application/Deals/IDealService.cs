using HotelBooking.Application.Deals.Dtos;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Deals;

public interface IDealService
{
    Task<Result<IReadOnlyList<FeaturedDealDto>>> ListFeaturedAsync(
        FeaturedDealsRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<DealDto>> CreateAsync(
        CreateDealRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<DealDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<DealDto>> UpdateAsync(
        Guid id,
        UpdateDealRequest request,
        string? ifMatch,
        CancellationToken cancellationToken = default);

    Task<Result<Deleted>> DeleteAsync(
        Guid id,
        string? ifMatch,
        CancellationToken cancellationToken = default);
}
