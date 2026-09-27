using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Bookings;

public sealed class BookingLine
{
    private BookingLine(
        Guid bookingId,
        int lineNumber,
        Guid roomId,
        DateRange stay,
        Occupancy guests,
        Money nightlyRate,
        Money lineTotal,
        Money discountTotal)
    {
        BookingId = bookingId;
        LineNumber = lineNumber;
        RoomId = roomId;
        Stay = stay;
        Guests = guests;
        NightlyRate = nightlyRate;
        LineTotal = lineTotal;
        DiscountTotal = discountTotal;
    }

    private BookingLine()
    {
        Stay = null!;
        Guests = null!;
        NightlyRate = null!;
        LineTotal = null!;
        DiscountTotal = null!;
    }

    public Guid BookingId { get; private set; }

    public int LineNumber { get; private set; }

    public Guid RoomId { get; private set; }

    public DateRange Stay { get; private set; }

    public Occupancy Guests { get; private set; }

    public Money NightlyRate { get; private set; }

    public Money LineTotal { get; private set; }

    public Money DiscountTotal { get; private set; }

    internal static Result<BookingLine> For(Guid bookingId, int lineNumber, RoomStay requested)
    {
        var (room, stay, guests, deals) = requested;

        if (!room.CanHost(guests))
        {
            return BookingErrors.RoomCannotHostParty;
        }

        var price = BookingPricingService.PriceStay(room, stay, deals);

        return price.IsError
            ? price.Errors
            : new BookingLine(
                bookingId,
                lineNumber,
                room.Id,
                stay,
                guests,
                room.BasePrice,
                price.Value.Total,
                price.Value.Discount);
    }

    internal IEnumerable<RoomNight> SellNights() =>
        Stay.EachNight().Select(night => RoomNight.Sell(RoomId, night, BookingId));

    internal bool ClaimsANightAlsoClaimedBy(BookingLine other) =>
        RoomId == other.RoomId
        && Stay.CheckIn < other.Stay.CheckOut
        && other.Stay.CheckIn < Stay.CheckOut;
}
