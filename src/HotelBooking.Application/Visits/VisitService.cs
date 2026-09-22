using HotelBooking.Application.Hotels;
using HotelBooking.Application.Hotels.Dtos;
using HotelBooking.Domain.Results;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Visits;

public sealed class VisitService(
    IVisitRankings visitRankings,
    IHotelQueries hotelQueries,
    ILogger<VisitService> logger) : IVisitService
{
    public async Task<Result<IReadOnlyList<HotelSummaryDto>>> GetRecentlyViewedAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var viewed = await visitRankings.RecentlyViewedHotelIdsAsync(
            userId, VisitLimits.RecentHotelsKept, cancellationToken);

        if (viewed.IsError)
        {
            logger.LogWarning(
                "The recently-visited list of user {UserId} could not be read, so the home page "
                + "was served without it",
                userId);

            return Empty;
        }

        if (viewed.Value.Count == 0)
        {
            return Empty;
        }

        var cards = await hotelQueries.ListCardsAsync(viewed.Value, cancellationToken);

        List<HotelSummaryDto> recent =
        [
            .. viewed.Value
                .Select(id => cards.FirstOrDefault(card => card.Id == id))
                .OfType<HotelSummaryDto>()
        ];

        logger.LogDebug(
            "Served {HotelCount} of the {ViewedCount} hotels user {UserId} last viewed",
            recent.Count, viewed.Value.Count, userId);

        return recent;
    }

    private static Result<IReadOnlyList<HotelSummaryDto>> Empty =>
        Result<IReadOnlyList<HotelSummaryDto>>.From([]);
}
