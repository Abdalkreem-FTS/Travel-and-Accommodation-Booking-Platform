using HotelBooking.Application.Deals;
using HotelBooking.Application.Deals.Dtos;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence.Queries;

internal sealed class DealQueries(HotelBookingDbContext context) : IDealQueries
{
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
