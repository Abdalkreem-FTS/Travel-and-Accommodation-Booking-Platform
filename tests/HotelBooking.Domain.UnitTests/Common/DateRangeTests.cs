using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.UnitTests.Common;

public sealed class DateRangeTests
{
    private static readonly DateOnly Today = new(2026, 9, 17);

    private static Result<DateRange> Create(int checkInOffset = 1, int nights = 3) =>
        DateRange.Create(
            Today.AddDays(checkInOffset),
            Today.AddDays(checkInOffset + nights),
            Today);

    [Fact]
    public void Create_ForAThreeNightStay_CountsTheNightsBetweenTheDatesNotTheDates()
    {
        var stay = Create(nights: 3).Value;

        stay.Nights.ShouldBe(3);
        stay.EachNight().ShouldBe([Today.AddDays(1), Today.AddDays(2), Today.AddDays(3)]);
    }

    [Fact]
    public void Create_WithCheckOutOnOrBeforeCheckIn_IsRejectedBecauseItSellsNoNights()
    {
        DateRange.Create(Today.AddDays(2), Today.AddDays(2), Today)
            .Errors.ShouldContain(DateRangeErrors.CheckOutNotAfterCheckIn);

        DateRange.Create(Today.AddDays(2), Today.AddDays(1), Today)
            .Errors.ShouldContain(DateRangeErrors.CheckOutNotAfterCheckIn);
    }

    [Fact]
    public void Create_WithCheckInBeforeToday_IsRejectedAgainstTheInjectedDateNotTheSystemClock()
    {
        Create(checkInOffset: -1).Errors.ShouldContain(DateRangeErrors.CheckInInThePast);

        Create(checkInOffset: 0).IsSuccess.ShouldBeTrue("a stay starting today is still bookable");
    }

}
