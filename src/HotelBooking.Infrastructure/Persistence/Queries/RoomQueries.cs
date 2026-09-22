using HotelBooking.Application.Common;
using HotelBooking.Application.Rooms;
using HotelBooking.Application.Rooms.Dtos;
using HotelBooking.Domain.Rooms;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence.Queries;

internal sealed class RoomQueries(HotelBookingDbContext context) : IRoomQueries
{
    public async Task<PagedList<AvailableRoomDto>> ListAvailableAsync(
        RoomAvailabilityCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var rooms = Matching(criteria);

        var totalCount = await rooms.CountAsync(cancellationToken);

        var rows = await rooms
            .OrderBy(room => room.BasePrice.Amount)
            .ThenBy(room => room.Id)
            .Skip(criteria.Skip)
            .Take(criteria.PageSize)
            .Select(room => new
            {
                room.Id,
                room.Number,
                room.Type,
                room.Capacity.Adults,
                room.Capacity.Children,
                room.BasePrice.Amount,
                room.BasePrice.Currency
            })
            .ToListAsync(cancellationToken);

        List<AvailableRoomDto> items =
        [
            .. rows.Select(row => new AvailableRoomDto(
                row.Id,
                row.Number,
                row.Type.ToString(),
                row.Adults,
                row.Children,
                row.Amount,
                row.Currency))
        ];

        return new PagedList<AvailableRoomDto>(items, criteria.Page, criteria.PageSize, totalCount);
    }

    private IQueryable<Room> Matching(RoomAvailabilityCriteria criteria)
    {
        var hotelId = criteria.HotelId;
        var adults = criteria.Guests.Adults;
        var children = criteria.Guests.Children;

        var rooms = context.Rooms
            .AsNoTracking()
            .Where(room => room.HotelId == hotelId
                && room.Capacity.Adults >= adults
                && room.Capacity.Children >= children);

        if (criteria.Stay is not { } stay)
        {
            return rooms;
        }

        var checkIn = stay.CheckIn;
        var checkOut = stay.CheckOut;

        return rooms.Where(room => !context.RoomNightInventory.Any(night =>
            night.RoomId == room.Id && night.StayDate >= checkIn && night.StayDate < checkOut));
    }
}
