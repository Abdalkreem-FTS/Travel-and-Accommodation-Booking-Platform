using System.Diagnostics;

using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Common;
using HotelBooking.Application.Hotels.Dtos;
using HotelBooking.Domain.Results;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Hotels;

public sealed class HotelSearchService(
    IHotelQueries hotelQueries,
    ICacheService cacheService,
    IDateTimeProvider dateTimeProvider,
    ILogger<HotelSearchService> logger) : IHotelSearchService
{
    public async Task<Result<PagedList<HotelSummaryDto>>> SearchAsync(
        HotelSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var criteria = HotelSearchCriteria.Create(
            request, DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime));

        if (criteria.IsError)
        {
            return criteria.Errors;
        }

        var startedAt = Stopwatch.GetTimestamp();

        var page = (await cacheService.GetOrSetAsync(
            HotelCacheKeys.Search(criteria.Value),
            async token => await hotelQueries.SearchAsync(criteria.Value, token),
            HotelCacheKeys.SearchTimeToLive,
            cancellationToken))!;

        Telemetry.SearchDuration.Record(
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            new KeyValuePair<string, object?>("dated", criteria.Value.Stay is not null));

        Telemetry.HotelSearches.Add(
            1,
            new KeyValuePair<string, object?>("sort", criteria.Value.Sort.ToString()),
            new KeyValuePair<string, object?>("dated", criteria.Value.Stay is not null));

        logger.LogDebug(
            "Searched hotels: page {Page} of {TotalPages}, {TotalCount} matched, sorted by {Sort}",
            page.Page, page.TotalPages, page.TotalCount, criteria.Value.Sort);

        return page;
    }
}
