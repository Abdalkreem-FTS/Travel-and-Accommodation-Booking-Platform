using System.Globalization;

using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

namespace HotelBooking.Domain.Carts;

public sealed class CartItem
{
    private const string DayFormat = "yyyyMMdd";

    private CartItem(
        Guid roomId,
        Guid hotelId,
        DateRange stay,
        Occupancy guests,
        Money nightlyRate,
        Money total)
    {
        RoomId = roomId;
        HotelId = hotelId;
        Stay = stay;
        Guests = guests;
        NightlyRate = nightlyRate;
        Total = total;
    }

    public Guid RoomId { get; }

    public Guid HotelId { get; }

    public DateRange Stay { get; }

    public Occupancy Guests { get; }

    public Money NightlyRate { get; }

    public Money Total { get; }

    public string Id => IdFor(RoomId, Stay);

    public static string IdFor(Guid roomId, DateRange stay) => string.Create(
        CultureInfo.InvariantCulture,
        $"{roomId:N}-{stay.CheckIn.ToString(DayFormat, CultureInfo.InvariantCulture)}"
        + $"-{stay.CheckOut.ToString(DayFormat, CultureInfo.InvariantCulture)}");

    public static Result<CartItem> For(Room room, DateRange stay, Occupancy guests)
    {
        if (!room.CanHost(guests))
        {
            return CartErrors.RoomCannotHostParty;
        }

        return Restore(room.Id, room.HotelId, stay, guests, room.BasePrice);
    }

    public static Result<CartItem> Restore(
        Guid roomId,
        Guid hotelId,
        DateRange stay,
        Occupancy guests,
        Money nightlyRate)
    {
        var total = BookingPricingService.PriceStay(nightlyRate, stay);

        return total.IsError
            ? total.Errors
            : new CartItem(roomId, hotelId, stay, guests, nightlyRate, total.Value);
    }

    public bool IsStillBookable(DateOnly today) =>
        DateRange.Create(Stay.CheckIn, Stay.CheckOut, today).IsSuccess;
}
