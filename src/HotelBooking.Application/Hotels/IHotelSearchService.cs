using HotelBooking.Application.Common;
using HotelBooking.Application.Hotels.Dtos;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Hotels;

public interface IHotelSearchService
{
    Task<Result<PagedList<HotelSummaryDto>>> SearchAsync(
        HotelSearchRequest request,
        CancellationToken cancellationToken = default);
}
