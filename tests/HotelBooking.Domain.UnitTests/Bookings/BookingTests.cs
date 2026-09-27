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

    private static Room ARoom(Guid? id = null, Guid? hotelId = null, decimal nightly = 120.50m) =>
        Room.Create(
            id ?? FirstRoomId,
            hotelId ?? HotelId,
            "1203",
            RoomType.Luxury,
            Occupancy.Create(2, 1).Value,
            Money.Create(nightly, "USD").Value,
            Now).Value;

    private static DateRange AStay(int nights = 3, int startingIn = 1) =>
        DateRange.Create(Today.AddDays(startingIn), Today.AddDays(startingIn + nights), Today).Value;

    private static RoomStay AStayIn(Room? room = null, DateRange? stay = null) =>
        new(room ?? ARoom(), stay ?? AStay(), Occupancy.Create(2, 1).Value);

    private static Result<Booking> Reserve(params RoomStay[] stays) =>
        Booking.Reserve(
            BookingId,
            UserId,
            stays.Length > 0 ? stays : [AStayIn()],
            ConfirmationNumber.From(EventId),
            Now);

    [Fact]
    public void Reserve_ForTwoRooms_HoldsALinePerStayAndTotalsWhatEachLineCosts()
    {
        var booking = Reserve(
            AStayIn(ARoom(FirstRoomId, nightly: 120.50m), AStay(nights: 3)),
            AStayIn(ARoom(SecondRoomId, nightly: 80m), AStay(nights: 2))).Value;

        booking.Lines.Select(line => line.LineNumber).ShouldBe([1, 2]);
        booking.Lines[0].LineTotal.ShouldBe(Money.Create(361.50m, "USD").Value);
        booking.Lines[1].LineTotal.ShouldBe(Money.Create(160m, "USD").Value);
        booking.TotalPrice.ShouldBe(Money.Create(521.50m, "USD").Value);
        booking.HotelId.ShouldBe(HotelId);
        booking.Status.ShouldBe(BookingStatus.Pending);
        booking.DomainEvents.ShouldBeEmpty("the confirmation email waits for the money");
        booking.CreatedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void Reserve_ForAThreeNightStay_SellsOneLedgerRowPerNightAndNoneForTheCheckOutDate()
    {
        var stay = AStay(nights: 3);

        var nights = Reserve(AStayIn(stay: stay)).Value.Nights;

        nights.Select(night => night.StayDate).ShouldBe(
            [stay.CheckIn, stay.CheckIn.AddDays(1), stay.CheckIn.AddDays(2)]);
        nights.ShouldAllBe(night => night.RoomId == FirstRoomId && night.BookingId == BookingId);
    }

    [Fact]
    public void Reserve_ForSeveralRooms_SellsTheNightsInRoomThenDateOrderWhateverOrderTheyWereAskedFor()
    {
        var booking = Reserve(
            AStayIn(ARoom(SecondRoomId), AStay(nights: 2)),
            AStayIn(ARoom(FirstRoomId), AStay(nights: 2))).Value;

        booking.Nights.ShouldBe(
            [.. booking.Nights.OrderBy(night => night.RoomId).ThenBy(night => night.StayDate)],
            "every checkout must take the ledger rows in one order or two of them can deadlock");
    }

    [Fact]
    public void Reserve_ForAStay_SnapshotsTheNightlyRateSoRepricingTheRoomLaterLeavesTheBookingAlone()
    {
        var room = ARoom(nightly: 120.50m);

        var booking = Reserve(AStayIn(room, AStay(nights: 3))).Value;

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
    public void Reserve_ForRoomsInDifferentHotels_IsRefusedBecauseOneBookingIsOneStay()
    {
        var result = Reserve(
            AStayIn(ARoom(FirstRoomId, HotelId)),
            AStayIn(ARoom(SecondRoomId, new Guid("00000000-0000-0000-0003-000000000002"))));

        result.TopError.ShouldBe(BookingErrors.LinesSpanHotels);
    }

    [Fact]
    public void Reserve_ForTheSameRoomOverNightsItAlreadyClaims_IsRefusedRatherThanFightingItselfInTheLedger()
    {
        var result = Reserve(
            AStayIn(ARoom(FirstRoomId), AStay(nights: 3, startingIn: 1)),
            AStayIn(ARoom(FirstRoomId), AStay(nights: 3, startingIn: 3)));

        result.TopError.ShouldBe(BookingErrors.OverlappingLines);
    }

    [Fact]
    public void Confirm_RaisesBookingConfirmedCarryingWhatTheEmailWillNeed()
    {
        var booking = Reserve(
            AStayIn(ARoom(FirstRoomId), AStay(nights: 2, startingIn: 1)),
            AStayIn(ARoom(SecondRoomId), AStay(nights: 2, startingIn: 3))).Value;

        booking.Confirm(EventId, Now).IsSuccess.ShouldBeTrue();

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

    [Fact]
    public void Expire_WhenThePaymentNeverCame_ReleasesTheNights()
    {
        var booking = Reserve().Value;

        booking.Expire().IsSuccess.ShouldBeTrue();

        booking.Status.ShouldBe(BookingStatus.Expired);
        booking.Nights.ShouldBeEmpty();
    }

    [Fact]
    public void Expire_OnAConfirmedBooking_IsRefusedSoAPaidStayNeverLosesItsNights()
    {
        var booking = Reserve().Value;
        booking.Confirm(EventId, Now).IsSuccess.ShouldBeTrue();

        booking.Expire().TopError.ShouldBe(BookingErrors.InvalidTransition);

        booking.Nights.ShouldNotBeEmpty();
    }

    [Fact]
    public void Cancel_RaisesBookingCancelledSoTheGuestIsTold()
    {
        var booking = Reserve(AStayIn(stay: AStay(startingIn: 10))).Value;
        booking.Confirm(EventId, Now).IsSuccess.ShouldBeTrue();
        booking.ClearDomainEvents();

        var cancelEventId = new Guid("00000000-0000-0000-0006-000000000002");

        booking.Cancel(cancelEventId, Now).IsSuccess.ShouldBeTrue();

        var cancelled = booking.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<BookingCancelled>();
        cancelled.EventId.ShouldBe(cancelEventId);
        cancelled.BookingId.ShouldBe(BookingId);
        cancelled.Reason.ShouldBe(CancellationReason.RequestedByGuest);
    }
}
