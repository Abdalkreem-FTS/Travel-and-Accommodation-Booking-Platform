using HotelBooking.Application.Cities;
using HotelBooking.Application.Cities.Dtos;
using HotelBooking.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence.Queries;

internal sealed class CityQueries(HotelBookingDbContext context) : ICityQueries
{
    public async Task<PagedList<CitySummaryDto>> ListAsync(
        CityListCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var cities = context.Cities.AsNoTracking();

        if (criteria.Search is { } search)
        {
            cities = cities.Where(city => EF.Functions.Like(city.Name, $"%{search}%"));
        }

        var totalCount = await cities.CountAsync(cancellationToken);

        var rows = await cities
            .OrderBy(city => city.Name)
            .ThenBy(city => city.Id)
            .Skip(criteria.Skip)
            .Take(criteria.PageSize)
            .Select(city => new
            {
                city.Id,
                city.Name,
                city.Country,
                city.PostOffice,
                city.ThumbnailUrl,
                HotelCount = context.Hotels.Count(hotel => hotel.CityId == city.Id),
                city.CreatedAtUtc,
                city.ModifiedAtUtc,
                RowVersion = EF.Property<byte[]>(city, RowVersionProperty.Name)
            })
            .ToListAsync(cancellationToken);

        List<CitySummaryDto> items =
        [
            .. rows.Select(row => new CitySummaryDto(
                row.Id,
                row.Name,
                row.Country.Value,
                row.PostOffice,
                row.ThumbnailUrl,
                row.HotelCount,
                row.CreatedAtUtc,
                row.ModifiedAtUtc,
                ConcurrencyToken.From(row.RowVersion).Version))
        ];

        return new PagedList<CitySummaryDto>(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<CitySummaryDto>> ListByIdsAsync(
        IReadOnlyList<Guid> cityIds,
        CancellationToken cancellationToken = default)
    {
        if (cityIds.Count == 0)
        {
            return [];
        }

        var rows = await context.Cities
            .AsNoTracking()
            .Where(city => cityIds.Contains(city.Id))
            .Select(city => new
            {
                city.Id,
                city.Name,
                city.Country,
                city.PostOffice,
                city.ThumbnailUrl,
                HotelCount = context.Hotels.Count(hotel => hotel.CityId == city.Id),
                city.CreatedAtUtc,
                city.ModifiedAtUtc,
                RowVersion = EF.Property<byte[]>(city, RowVersionProperty.Name)
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new CitySummaryDto(
                row.Id,
                row.Name,
                row.Country.Value,
                row.PostOffice,
                row.ThumbnailUrl,
                row.HotelCount,
                row.CreatedAtUtc,
                row.ModifiedAtUtc,
                ConcurrencyToken.From(row.RowVersion).Version))
        ];
    }
}
