using HotelBooking.Application.Common;
using HotelBooking.Application.Deals;
using HotelBooking.Application.Deals.Dtos;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence.Queries;

internal sealed class DealQueries(HotelBookingDbContext context) : IDealQueries
{
    public async Task<PagedList<DealDto>> ListForRoomAsync(
        Guid roomId,
        PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        var deals = context.Deals.AsNoTracking().Where(deal => deal.RoomId == roomId);

        var totalCount = await deals.CountAsync(cancellationToken);

        var rows = await deals
            .OrderByDescending(deal => deal.StartsOn)
            .ThenByDescending(deal => deal.EndsOn)
            .ThenByDescending(deal => deal.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(deal => new
            {
                deal.Id,
                deal.HotelId,
                deal.RoomId,
                DiscountPercentage = deal.Discount.Value,
                deal.StartsOn,
                deal.EndsOn,
                deal.IsFeatured,
                deal.CreatedAtUtc,
                deal.ModifiedAtUtc,
                RowVersion = EF.Property<byte[]>(deal, RowVersionProperty.Name)
            })
            .ToListAsync(cancellationToken);

        List<DealDto> items =
        [
            .. rows.Select(row => new DealDto(
                row.Id,
                row.HotelId,
                row.RoomId,
                row.DiscountPercentage,
                row.StartsOn,
                row.EndsOn,
                row.IsFeatured,
                row.CreatedAtUtc,
                row.ModifiedAtUtc,
                ConcurrencyToken.From(row.RowVersion).Version))
        ];

        return new PagedList<DealDto>(items, paging.Page, paging.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<FeaturedDealRow>> ListFeaturedAsync(
        int limit,
        DateOnly onDate,
        CancellationToken cancellationToken = default)
    {
        var deals =
            from deal in context.Deals.AsNoTracking()
            where deal.IsFeatured && deal.StartsOn <= onDate && deal.EndsOn > onDate
            join room in context.Rooms.AsNoTracking() on deal.RoomId equals room.Id
            join hotel in context.Hotels.AsNoTracking() on deal.HotelId equals hotel.Id
            join city in context.Cities.AsNoTracking() on hotel.CityId equals city.Id
            orderby deal.Discount.Value descending, deal.Id
            select new FeaturedDealRow(
                deal.Id,
                hotel.Id,
                hotel.Name,
                city.Id,
                city.Name,
                hotel.ThumbnailUrl,
                hotel.StarRating.Value,
                room.Id,
                room.Type,
                room.BasePrice.Amount,
                room.BasePrice.Currency,
                deal.Discount.Value,
                deal.EndsOn);

        return await deals.Take(limit).ToListAsync(cancellationToken);
    }
}
