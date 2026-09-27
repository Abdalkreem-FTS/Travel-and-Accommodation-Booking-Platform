using HotelBooking.Domain.Deals;

namespace HotelBooking.Domain.UnitTests.Deals;

public sealed class DealTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Start = new(2026, 10, 1);

    private static readonly Guid DealId = new("00000000-0000-0000-0005-000000000001");
    private static readonly Guid HotelId = new("00000000-0000-0000-0003-000000000001");
    private static readonly Guid RoomId = new("00000000-0000-0000-0004-000000000001");

    private static DiscountPercentage TenPercent => DiscountPercentage.Create(10).Value;

    private static Deal ADeal(int nights = 3) =>
        Deal.Create(DealId, HotelId, RoomId, TenPercent, Start, Start.AddDays(nights), false, Now).Value;

    private static DateOnly[] NightsOf(Deal deal) => [.. deal.Nights.Select(night => night.StayDate)];

    [Fact]
    public void Create_ClaimsOneNightPerDayFromTheStartUpToButNotIncludingTheEnd()
    {
        var deal = ADeal(nights: 3);

        NightsOf(deal).ShouldBe([Start, Start.AddDays(1), Start.AddDays(2)]);
        deal.Nights.ShouldAllBe(night => night.RoomId == RoomId && night.DealId == DealId);
        deal.IsLiveOn(Start.AddDays(3)).ShouldBeFalse("the end date is the first night without the discount");
    }

    [Fact]
    public void Create_WithAWindowThatNeverRunsOrRunsTooLong_IsRefused()
    {
        Deal.Create(DealId, HotelId, RoomId, TenPercent, Start, Start, false, Now)
            .Errors.ShouldContain(DealErrors.WindowEndsBeforeItStarts);

        Deal.Create(DealId, HotelId, RoomId, TenPercent, Start, Start.AddDays(Deal.MaxNights + 1), false, Now)
            .Errors.ShouldContain(DealErrors.WindowTooLong);

        Deal.Create(DealId, HotelId, RoomId, TenPercent, Start, Start.AddDays(Deal.MaxNights), false, Now)
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Update_ToAShiftedWindow_ReleasesTheNightsItLeftAndClaimsTheNewOnes()
    {
        var deal = ADeal(nights: 3);

        var result = deal.Update(TenPercent, Start.AddDays(2), Start.AddDays(5), true, Now);

        result.IsSuccess.ShouldBeTrue();
        NightsOf(deal).ShouldBe([Start.AddDays(2), Start.AddDays(3), Start.AddDays(4)]);
    }

    [Fact]
    public void Delete_ReleasesEveryNightAndRefusesAnyLaterChange()
    {
        var deal = ADeal();

        deal.Delete(Now).IsSuccess.ShouldBeTrue();

        deal.Nights.ShouldBeEmpty();
        deal.Update(TenPercent, Start, Start.AddDays(2), false, Now).TopError.ShouldBe(DealErrors.AlreadyDeleted);
        deal.Delete(Now).TopError.ShouldBe(DealErrors.AlreadyDeleted);
    }
}
