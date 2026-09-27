using HotelBooking.Application.Common;
using HotelBooking.Application.Hotels;
using HotelBooking.Application.Hotels.Dtos;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Rooms;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence.Queries;

internal sealed class HotelQueries(HotelBookingDbContext context) : IHotelQueries
{
    public async Task<PagedList<HotelSummaryDto>> SearchAsync(
        HotelSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var rooms = MatchingRooms(criteria);

        var matching =
            from hotel in MatchingHotels(criteria)
            join city in context.Cities.AsNoTracking() on hotel.CityId equals city.Id
            where rooms.Any(room => room.HotelId == hotel.Id)
            select hotel.Id;

        var cards =
            from hotel in MatchingHotels(criteria)
            join city in context.Cities.AsNoTracking() on hotel.CityId equals city.Id
            from cheapest in rooms
                .Where(room => room.HotelId == hotel.Id)
                .OrderBy(room => room.BasePrice.Amount)
                .Take(1)
            select new
            {
                hotel.Id,
                hotel.Name,
                hotel.CityId,
                CityName = city.Name,
                hotel.StarRating,
                hotel.ThumbnailUrl,
                FromPrice = cheapest.BasePrice.Amount,
                Currency = cheapest.BasePrice.Currency
            };

        var totalCount = await matching.CountAsync(cancellationToken);

        var ordered = criteria.Sort switch
        {
            HotelSort.Price => cards.OrderBy(card => card.FromPrice).ThenBy(card => card.Id),
            HotelSort.Stars => cards.OrderByDescending(card => card.StarRating).ThenBy(card => card.Id),
            _ => cards.OrderBy(card => card.Name).ThenBy(card => card.Id)
        };

        var rows = await ordered
            .Skip(criteria.Skip)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        List<HotelSummaryDto> items =
        [
            .. rows.Select(row => new HotelSummaryDto(
                row.Id,
                row.Name,
                row.CityId,
                row.CityName,
                row.StarRating.Value,
                row.ThumbnailUrl,
                row.FromPrice,
                row.Currency))
        ];

        return new PagedList<HotelSummaryDto>(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<HotelSummaryDto>> ListCardsAsync(
        IReadOnlyList<Guid> hotelIds,
        CancellationToken cancellationToken = default)
    {
        if (hotelIds.Count == 0)
        {
            return [];
        }

        var rooms = context.Rooms.AsNoTracking();

        var cards =
            from hotel in context.Hotels.AsNoTracking()
            where hotelIds.Contains(hotel.Id)
            join city in context.Cities.AsNoTracking() on hotel.CityId equals city.Id
            from cheapest in rooms
                .Where(room => room.HotelId == hotel.Id)
                .OrderBy(room => room.BasePrice.Amount)
                .Take(1)
            select new
            {
                hotel.Id,
                hotel.Name,
                hotel.CityId,
                CityName = city.Name,
                hotel.StarRating,
                hotel.ThumbnailUrl,
                FromPrice = cheapest.BasePrice.Amount,
                Currency = cheapest.BasePrice.Currency
            };

        var rows = await cards.ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new HotelSummaryDto(
                row.Id,
                row.Name,
                row.CityId,
                row.CityName,
                row.StarRating.Value,
                row.ThumbnailUrl,
                row.FromPrice,
                row.Currency))
        ];
    }

    private IQueryable<Hotel> MatchingHotels(HotelSearchCriteria criteria)
    {
        var hotels = context.Hotels.AsNoTracking();

        if (criteria.CityId is { } cityId)
        {
            hotels = hotels.Where(hotel => hotel.CityId == cityId);
        }

        if (criteria.Stars.Count <= 0)
        {
            return hotels;
        }

        var stars = criteria.Stars;

        hotels = hotels.Where(hotel => stars.Contains(hotel.StarRating));

        return hotels;
    }

    private IQueryable<Room> MatchingRooms(HotelSearchCriteria criteria)
    {
        var adults = criteria.Guests.Adults;
        var children = criteria.Guests.Children;

        var rooms = context.Rooms
            .AsNoTracking()
            .Where(room => room.Capacity.Adults >= adults && room.Capacity.Children >= children);

        if (criteria.RoomType is { } roomType)
        {
            rooms = rooms.Where(room => room.Type == roomType);
        }

        if (criteria.MinPrice is { } minPrice)
        {
            rooms = rooms.Where(room => room.BasePrice.Amount >= minPrice);
        }

        if (criteria.MaxPrice is { } maxPrice)
        {
            rooms = rooms.Where(room => room.BasePrice.Amount <= maxPrice);
        }

        if (criteria.Stay is not { } stay)
        {
            return rooms;
        }

        var checkIn = stay.CheckIn;
        var checkOut = stay.CheckOut;

        rooms = rooms.Where(room => !context.RoomNightInventory.Any(night =>
            night.RoomId == room.Id && night.StayDate >= checkIn && night.StayDate < checkOut));

        return rooms;
    }
}
