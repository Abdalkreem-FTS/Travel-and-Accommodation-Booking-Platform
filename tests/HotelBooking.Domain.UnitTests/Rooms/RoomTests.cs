using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

namespace HotelBooking.Domain.UnitTests.Rooms;

public sealed class RoomTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid RoomId = new("00000000-0000-0000-0004-000000000001");
    private static readonly Guid HotelId = new("00000000-0000-0000-0003-000000000001");

    private static Occupancy For(int adults, int children) => Occupancy.Create(adults, children).Value;

    private static Result<Room> Create(decimal price) =>
        Room.Create(RoomId, HotelId, "1203", RoomType.Luxury, For(2, 1), Money.Create(price, "USD").Value, Now);

    [Fact]
    public void Create_AtAPriceOfZero_IsRefused()
    {
        Create(0m).Errors.ShouldContain(RoomErrors.BasePriceMustBePositive);
    }

    [Fact]
    public void CanHost_OnceTheRoomIsDeleted_IsFalseEvenForAPartyThatFits()
    {
        var room = Create(120m).Value;
        room.CanHost(For(2, 1)).ShouldBeTrue();

        room.Delete(Now);

        room.CanHost(For(2, 1)).ShouldBeFalse();
    }
}
