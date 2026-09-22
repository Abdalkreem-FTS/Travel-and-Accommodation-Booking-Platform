using HotelBooking.Application.Hotels.Dtos;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Visits;

public interface IVisitService
{
    Task<Result<IReadOnlyList<HotelSummaryDto>>> GetRecentlyViewedAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
