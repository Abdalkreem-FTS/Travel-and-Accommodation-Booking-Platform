using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Bookings.Events;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

namespace HotelBooking.Domain.UnitTests.Bookings;

public sealed class BookingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 17);

    private static readonly Guid BookingId = new("00000000-0000-0000-0005-000000000001");
    private static readonly Guid EventId = new("00000000-0000-0000-0006-000000000001");
    private static readonly Guid UserId = new("00000000-0000-0000-0001-000000000001");
    private static readonly Guid HotelId = new("00000000-0000-0000-0003-000000000001");

    private static readonly Guid FirstRoomId = new("00000000-0000-0000-0004-000000000001");
    private static readonly Guid SecondRoomId = new("00000000-0000-0000-0004-000000000002");

    private static Room ARoom(
        Guid? id = null,
        Guid? hotelId = null,
        decimal nightly = 120.50m,
        string currency = "USD",
        int adults = 2,
        int children = 1) =>
        Room.Create(
            id ?? FirstRoomId,
            hotelId ?? HotelId,
            "1203",
            RoomType.Luxury,
            Occupancy.Create(adults, children).Value,
            Money.Create(nightly, currency).Value,
            Now).Value;

    private static DateRange AStay(int nights = 3, int startingIn = 1) =>
        DateRange.Create(Today.AddDays(startingIn), Today.AddDays(startingIn + nights), Today).Value;

    private static RoomStay AStayIn(
        Room? room = null,
        DateRange? stay = null,
        Occupancy? guests = null) =>
        new(room ?? ARoom(), stay ?? AStay(), guests ?? Occupancy.Create(2, 1).Value);

    private static Result<Booking> Create(params RoomStay[] stays) =>
        Booking.Create(
            BookingId,
            UserId,
            stays.Length > 0 ? stays : [AStayIn()],
            ConfirmationNumber.From(EventId),
            EventId,
            Now);

    [Fact]
    public void Create_ForTwoRooms_HoldsALinePerStayAndTotalsWhatEachLineCosts()
    {
        var result = Create(
            AStayIn(ARoom(FirstRoomId, nightly: 120.50m), AStay(nights: 3)),
            AStayIn(ARoom(SecondRoomId, nightly: 80m), AStay(nights: 2)));

        result.IsSuccess.ShouldBeTrue();

        var booking = result.Value;

        booking.Lines.Count.ShouldBe(2);
        booking.Lines.Select(line => line.LineNumber).ShouldBe([1, 2]);
        booking.Lines[0].LineTotal.ShouldBe(Money.Create(361.50m, "USD").Value);
        booking.Lines[1].LineTotal.ShouldBe(Money.Create(160m, "USD").Value);
        booking.TotalPrice.ShouldBe(Money.Create(521.50m, "USD").Value);
        booking.HotelId.ShouldBe(HotelId);
        booking.Status.ShouldBe(BookingStatus.Confirmed);
        booking.CreatedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void Create_ForAThreeNightStay_SellsOneLedgerRowPerNightAndNoneForTheCheckOutDate()
    {
        var stay = AStay(nights: 3);

        var nights = Create(AStayIn(stay: stay)).Value.Nights;

        nights.Count.ShouldBe(3);
        nights.Select(night => night.StayDate).ShouldBe(
            [stay.CheckIn, stay.CheckIn.AddDays(1), stay.CheckIn.AddDays(2)]);
        nights.ShouldAllBe(night => night.RoomId == FirstRoomId && night.BookingId == BookingId);
        nights.ShouldNotContain(night => night.StayDate == stay.CheckOut);
    }

    [Fact]
    public void Create_ForSeveralRooms_SellsTheNightsInRoomThenDateOrderWhateverOrderTheyWereAskedFor()
    {
        var booking = Create(
            AStayIn(ARoom(SecondRoomId), AStay(nights: 2)),
            AStayIn(ARoom(FirstRoomId), AStay(nights: 2))).Value;

        booking.Nights.ShouldBe(
            [.. booking.Nights.OrderBy(night => night.RoomId).ThenBy(night => night.StayDate)],
            "every checkout must take the ledger rows in one order or two of them can deadlock");

        booking.Nights[0].RoomId.ShouldBe(FirstRoomId);
    }

    [Fact]
    public void Create_ForAStay_SnapshotsTheNightlyRateSoRepricingTheRoomLaterLeavesTheBookingAlone()
    {
        var room = ARoom(nightly: 120.50m);

        var booking = Create(AStayIn(room, AStay(nights: 3))).Value;

        room.Update(
            room.Number,
            room.Type,
            room.Capacity,
            Money.Create(300m, "USD").Value,
            Now.AddDays(1)).IsSuccess.ShouldBeTrue();

        booking.Lines[0].NightlyRate.ShouldBe(Money.Create(120.50m, "USD").Value);
        booking.TotalPrice.ShouldBe(Money.Create(361.50m, "USD").Value);
    }

    [Fact]
    public void Create_ForRoomsInDifferentHotels_IsRefusedBecauseOneBookingIsOneStay()
    {
        var result = Create(
            AStayIn(ARoom(FirstRoomId, HotelId)),
            AStayIn(ARoom(SecondRoomId, new Guid("00000000-0000-0000-0003-000000000002"))));

        result.TopError.ShouldBe(BookingErrors.LinesSpanHotels);
    }

    [Fact]
    public void Create_ForTheSameRoomOverNightsItAlreadyClaims_IsRefusedRatherThanFightingItselfInTheLedger()
    {
        var result = Create(
            AStayIn(ARoom(FirstRoomId), AStay(nights: 3, startingIn: 1)),
            AStayIn(ARoom(FirstRoomId), AStay(nights: 3, startingIn: 3)));

        result.TopError.ShouldBe(BookingErrors.OverlappingLines);
    }

    [Fact]
    public void Create_OnSuccess_RaisesBookingConfirmedCarryingWhatTheEmailWillNeed()
    {
        var booking = Create(
            AStayIn(ARoom(FirstRoomId), AStay(nights: 2, startingIn: 1)),
            AStayIn(ARoom(SecondRoomId), AStay(nights: 2, startingIn: 3))).Value;

        var confirmed = booking.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<BookingConfirmed>();

        confirmed.EventId.ShouldBe(EventId);
        confirmed.BookingId.ShouldBe(BookingId);
        confirmed.UserId.ShouldBe(UserId);
        confirmed.HotelId.ShouldBe(HotelId);
        confirmed.ConfirmationNumber.ShouldBe(booking.Confirmation.Value);
        confirmed.CheckIn.ShouldBe(booking.EarliestCheckIn);
        confirmed.CheckOut.ShouldBe(booking.LatestCheckOut);
        confirmed.OccurredAtUtc.ShouldBe(Now);
    }
}
