using HotelBooking.Domain.Common;
using HotelBooking.Domain.Deals;
using HotelBooking.Domain.Rooms;

namespace HotelBooking.Domain.Bookings;

public sealed record RoomStay(Room Room, DateRange Stay, Occupancy Guests, IReadOnlyList<Deal> Deals)
{
    public RoomStay(Room room, DateRange stay, Occupancy guests)
        : this(room, stay, guests, [])
    {
    }
}
