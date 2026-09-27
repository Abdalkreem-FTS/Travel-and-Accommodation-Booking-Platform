using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Common;
using HotelBooking.Application.Rooms;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace HotelBooking.Application.UnitTests.Rooms;

public sealed class RoomServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 23, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 26);

    private static readonly Guid RoomId = new("00000000-0000-0000-0004-000000000001");
    private static readonly Guid HotelId = new("00000000-0000-0000-0003-000000000001");

    private static readonly string IfMatch = ConcurrencyToken.From(new byte[8]).ETag;

    private readonly IRoomRepository _rooms = Substitute.For<IRoomRepository>();
    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly Room _room;
    private readonly RoomService _service;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public RoomServiceTests()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        _room = Room.Create(
            RoomId, HotelId, "1203", RoomType.Luxury, Occupancy.Create(2, 0).Value,
            Money.Create(100m, "USD").Value, Now).Value;

        _rooms.GetByIdAsync(RoomId, Arg.Any<CancellationToken>()).Returns(_room);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);

        _service = new RoomService(
            _rooms, Substitute.For<IHotelRepository>(), _bookings, _unitOfWork,
            Substitute.For<IConcurrencyGuard>(), Substitute.For<IGuidProvider>(), clock,
            NullLogger<RoomService>.Instance);
    }

    [Fact]
    public async Task DeleteAsync_WhileItOwesAGuestTonightOrLater_IsRefusedAndTheRoomStays()
    {
        _bookings.HasNightsFromAsync(RoomId, Today, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.DeleteAsync(RoomId, IfMatch, Token);

        result.TopError.ShouldBe(RoomErrors.HasFutureBookings);
        _room.IsDeleted.ShouldBeFalse();
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(Token);
    }

    [Fact]
    public async Task DeleteAsync_WithOnlyPastNightsSold_DeletesTheRoom()
    {
        var result = await _service.DeleteAsync(RoomId, IfMatch, Token);

        result.IsSuccess.ShouldBeTrue();
        _room.IsDeleted.ShouldBeTrue();
        await _bookings.Received(1).HasNightsFromAsync(RoomId, Today, Arg.Any<CancellationToken>());
    }
}
