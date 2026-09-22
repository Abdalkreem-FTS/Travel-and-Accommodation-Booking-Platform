using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Cities.Dtos;
using HotelBooking.Application.Common;
using HotelBooking.Application.Visits;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Cities;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Results;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Cities;

public sealed class CityService(
    ICityRepository cityRepository,
    ICityQueries cityQueries,
    IHotelRepository hotelRepository,
    IVisitRankings visitRankings,
    IVisitTracker visitTracker,
    IUnitOfWork unitOfWork,
    IConcurrencyGuard concurrencyGuard,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ILogger<CityService> logger) : ICityService
{
    public async Task<Result<PagedList<CitySummaryDto>>> ListAsync(
        CityListRequest request,
        CancellationToken cancellationToken = default)
    {
        var criteria = CityListCriteria.Create(request);

        if (criteria.IsError)
        {
            return criteria.Errors;
        }

        if (criteria.Value.Sort is CitySort.Trending)
        {
            return await ListTrendingAsync(criteria.Value, cancellationToken);
        }

        var page = await cityQueries.ListAsync(criteria.Value, cancellationToken);

        logger.LogDebug(
            "Listed cities: page {Page} of {TotalPages}, {TotalCount} matched, searching for {Search}",
            page.Page, page.TotalPages, page.TotalCount, criteria.Value.Search);

        return page;
    }

    private async Task<Result<PagedList<CitySummaryDto>>> ListTrendingAsync(
        CityListCriteria criteria,
        CancellationToken cancellationToken)
    {
        var ranked = await visitRankings.TrendingCityIdsAsync(
            criteria.Skip, criteria.PageSize, cancellationToken);

        if (ranked.IsError)
        {
            logger.LogWarning(
                "Trending destinations were refused because the visit counters are unreachable");

            return CityListErrors.TrendingUnavailable;
        }

        var cities = await cityQueries.ListByIdsAsync(ranked.Value.CityIds, cancellationToken);

        List<CitySummaryDto> ordered =
        [
            .. ranked.Value.CityIds
                .Select(id => cities.FirstOrDefault(city => city.Id == id))
                .OfType<CitySummaryDto>()
        ];

        logger.LogDebug(
            "Listed trending cities: page {Page}, {Ranked} ranked, {Served} still in the catalogue",
            criteria.Page, ranked.Value.CityIds.Count, ordered.Count);

        return new PagedList<CitySummaryDto>(
            ordered, criteria.Page, criteria.PageSize, ranked.Value.TotalCount);
    }

    public async Task<Result<CityDto>> CreateAsync(
        CreateCityRequest request,
        CancellationToken cancellationToken = default)
    {
        var country = CountryCode.Create(request.Country);

        if (country.IsError)
        {
            return country.Errors;
        }

        var city = City.Create(
            guidProvider.NewSortable(),
            request.Name,
            country.Value,
            request.PostOffice,
            request.ThumbnailUrl,
            dateTimeProvider.UtcNow);

        if (city.IsError)
        {
            return city.Errors;
        }

        cityRepository.Add(city.Value);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        logger.LogInformation("Created city {CityId} in {Country}", city.Value.Id, country.Value.Value);

        return CityDto.From(city.Value, concurrencyGuard.TokenFor(city.Value));
    }

    public async Task<Result<CityDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var city = await cityRepository.GetByIdAsync(id, cancellationToken);

        return city is null
            ? CityErrors.NotFound
            : CityDto.From(city, concurrencyGuard.TokenFor(city));
    }

    public async Task<Result<CityDto>> UpdateAsync(
        Guid id,
        UpdateCityRequest request,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var version = ConcurrencyToken.Create(ifMatch);

        if (version.IsError)
        {
            return version.Errors;
        }

        var country = CountryCode.Create(request.Country);

        if (country.IsError)
        {
            return country.Errors;
        }

        var city = await cityRepository.GetByIdAsync(id, cancellationToken);

        if (city is null)
        {
            return CityErrors.NotFound;
        }

        concurrencyGuard.Expect(city, version.Value);

        var updated = city.Update(
            request.Name,
            country.Value,
            request.PostOffice,
            request.ThumbnailUrl,
            dateTimeProvider.UtcNow);

        if (updated.IsError)
        {
            return updated.Errors;
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        logger.LogInformation("Updated city {CityId}", id);

        return CityDto.From(city, concurrencyGuard.TokenFor(city));
    }

    public async Task<Result<Deleted>> DeleteAsync(
        Guid id,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var version = ConcurrencyToken.Create(ifMatch);

        if (version.IsError)
        {
            return version.Errors;
        }

        var city = await cityRepository.GetByIdAsync(id, cancellationToken);

        if (city is null)
        {
            return CityErrors.NotFound;
        }

        if (await hotelRepository.ExistsInCityAsync(id, cancellationToken))
        {
            Telemetry.CatalogDeletesRefused.Add(1, new KeyValuePair<string, object?>("resource", "city"));

            logger.LogInformation("Refused to delete city {CityId} because hotels still reference it", id);

            return CityErrors.HasHotels;
        }

        concurrencyGuard.Expect(city, version.Value);

        var deleted = city.Delete(dateTimeProvider.UtcNow);

        if (deleted.IsError)
        {
            return deleted.Errors;
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        await visitTracker.ForgetCityAsync(id, cancellationToken);

        logger.LogInformation("Deleted city {CityId}", id);

        return Result.Deleted;
    }
}
