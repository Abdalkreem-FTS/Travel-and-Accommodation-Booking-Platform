using HotelBooking.Application.Cities.Dtos;
using HotelBooking.Application.Common;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Cities;

public interface ICityService
{
    Task<Result<PagedList<CitySummaryDto>>> ListAsync(
        CityListRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CityDto>> CreateAsync(
        CreateCityRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CityDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<CityDto>> UpdateAsync(
        Guid id,
        UpdateCityRequest request,
        string? ifMatch,
        CancellationToken cancellationToken = default);

    Task<Result<Deleted>> DeleteAsync(
        Guid id,
        string? ifMatch,
        CancellationToken cancellationToken = default);
}
