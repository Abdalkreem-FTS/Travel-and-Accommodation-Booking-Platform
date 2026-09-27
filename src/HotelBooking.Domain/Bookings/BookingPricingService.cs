using HotelBooking.Domain.Common;
using HotelBooking.Domain.Deals;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

namespace HotelBooking.Domain.Bookings;

public static class BookingPricingService
{
    public static Result<Money> PriceStay(Room room, DateRange stay) =>
        PriceStay(room.BasePrice, stay);

    public static Result<Money> PriceStay(Money nightlyRate, DateRange stay) =>
        nightlyRate.Multiply(stay.Nights);

    public static Result<StayPrice> PriceStay(
        Room room,
        DateRange stay,
        IReadOnlyList<Deal> deals) =>
        PriceStay(room.BasePrice, room.Id, stay, deals);

    public static Result<StayPrice> PriceStay(
        Money nightlyRate,
        Guid roomId,
        DateRange stay,
        IReadOnlyList<Deal> deals)
    {
        List<Money> nights = [];

        foreach (var night in stay.EachNight())
        {
            var rate = RateFor(nightlyRate, roomId, night, deals);

            if (rate.IsError)
            {
                return rate.Errors;
            }

            nights.Add(rate.Value);
        }

        var charged = Sum(nights);

        if (charged.IsError)
        {
            return charged.Errors;
        }

        var undiscounted = PriceStay(nightlyRate, stay);

        if (undiscounted.IsError)
        {
            return undiscounted.Errors;
        }

        var saved = undiscounted.Value.Subtract(charged.Value);

        return saved.IsError
            ? saved.Errors
            : new StayPrice(charged.Value, saved.Value);
    }

    private static Result<Money> RateFor(
        Money nightlyRate,
        Guid roomId,
        DateOnly night,
        IReadOnlyList<Deal> deals)
    {
        var live = deals
            .Where(deal => deal.RoomId == roomId && deal.IsLiveOn(night))
            .OrderByDescending(deal => deal.Discount.Value)
            .ThenBy(deal => deal.Id)
            .FirstOrDefault();

        return live is null ? nightlyRate : live.Discount.ApplyTo(nightlyRate);
    }

    public static Result<Money> TotalFor(IReadOnlyList<RoomStay> stays)
    {
        List<Money> lineTotals = [];

        foreach (var stay in stays)
        {
            var lineTotal = PriceStay(stay.Room, stay.Stay, stay.Deals);

            if (lineTotal.IsError)
            {
                return lineTotal.Errors;
            }

            lineTotals.Add(lineTotal.Value.Total);
        }

        return Sum(lineTotals);
    }

    public static Result<Money> Sum(IReadOnlyList<Money> amounts)
    {
        if (amounts.Count == 0)
        {
            return BookingErrors.ItemsRequired;
        }

        var total = amounts[0];

        foreach (var amount in amounts.Skip(1))
        {
            var sum = total.Add(amount);

            if (sum.IsError)
            {
                return BookingErrors.CurrencyMismatch;
            }

            total = sum.Value;
        }

        return total;
    }
}
