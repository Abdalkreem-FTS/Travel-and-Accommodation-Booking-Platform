using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Rooms;

namespace HotelBooking.Domain.UnitTests.Bookings;

public sealed class BookingPricingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 17);

    private static Room ARoom(decimal nightly, string currency = "USD") =>
        Room.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "1203",
            RoomType.Luxury,
            Occupancy.Create(2, 1).Value,
            Money.Create(nightly, currency).Value,
            Now).Value;

    private static DateRange AStay(int nights) =>
        DateRange.Create(Today.AddDays(1), Today.AddDays(1 + nights), Today).Value;

    [Theory]
    [InlineData(1, 120.50)]
    [InlineData(3, 361.50)]
    public void PriceStay_ForAStay_ChargesTheNightlyRateOncePerNight(int nights, decimal expected)
    {
        var total = BookingPricingService.PriceStay(ARoom(120.50m), AStay(nights));

        total.Value.Amount.ShouldBe(expected);
    }

    [Fact]
    public void PriceStay_WithNoDeals_CostsExactlyWhatTheUndiscountedPriceSays()
    {
        var room = ARoom(120.50m);
        var stay = AStay(3);

        var price = BookingPricingService.PriceStay(room, stay, []);

        price.Value.Total.ShouldBe(BookingPricingService.PriceStay(room, stay).Value);
        price.Value.Discount.Amount.ShouldBe(0m);
    }

}
