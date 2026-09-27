using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Carts;
using HotelBooking.Application.Carts.Dtos;
using HotelBooking.Application.Carts.Validators;
using HotelBooking.Application.UnitTests.Common;
using HotelBooking.Domain.Carts;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Deals;
using HotelBooking.Domain.Rooms;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace HotelBooking.Application.UnitTests.Carts;

public sealed class CartServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 26);

    private static readonly Guid UserId = new("00000000-0000-0000-0001-000000000001");
    private static readonly Guid RoomId = new("00000000-0000-0000-0004-000000000001");
    private static readonly Guid HotelId = new("00000000-0000-0000-0003-000000000001");

    private readonly IRoomRepository _rooms = Substitute.For<IRoomRepository>();
    private readonly IDealRepository _deals = Substitute.For<IDealRepository>();
    private readonly FakeCartRepository _carts = new();
    private readonly CartService _service;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public CartServiceTests()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        _deals.ListLiveForRoomsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => []);

        _service = new CartService(
            _carts, _rooms, _deals, clock, new AddCartItemRequestValidator(), NullLogger<CartService>.Instance);
    }

    private static Room ADoubleRoom() =>
        Room.Create(
            RoomId, HotelId, "1203", RoomType.Standard, Occupancy.Create(2, 0).Value,
            Money.Create(100m, "USD").Value, Now).Value;

    private static AddCartItemRequest AStayFor(int adults) =>
        new(RoomId, Today.AddDays(1), Today.AddDays(3), adults, 0);

    [Fact]
    public async Task AddItemAsync_ForAPartyTheRoomCannotHost_IsRefusedAndTheCartStaysEmpty()
    {
        _rooms.GetByIdAsync(RoomId, Arg.Any<CancellationToken>()).Returns(ADoubleRoom());

        var result = await _service.AddItemAsync(AStayFor(adults: 3), UserId, Token);

        result.TopError.ShouldBe(CartErrors.RoomCannotHostParty);
        _carts.Held(UserId).ShouldBeEmpty();
    }

    [Fact]
    public async Task AddItemAsync_ForARoomThatDoesNotExist_AnswersNotFound()
    {
        var result = await _service.AddItemAsync(AStayFor(adults: 2), UserId, Token);

        result.TopError.ShouldBe(RoomErrors.NotFound);
        _carts.Held(UserId).ShouldBeEmpty();
    }

    [Fact]
    public async Task AddItemAsync_ForAPartyThatFits_HoldsTheStayPricedForEveryNight()
    {
        _rooms.GetByIdAsync(RoomId, Arg.Any<CancellationToken>()).Returns(ADoubleRoom());

        var result = await _service.AddItemAsync(AStayFor(adults: 2), UserId, Token);

        result.Value.ShouldSatisfyAllConditions(
            cart => cart.ItemCount.ShouldBe(1),
            cart => cart.TotalAmount.ShouldBe(200m, "two nights at 100"),
            cart => cart.Degraded.ShouldBeFalse());
        _carts.Held(UserId).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetAsync_WhenTheStoreIsDown_ServesAnEmptyCartMarkedDegradedInsteadOfFailing()
    {
        _carts.IsUnreachable = true;

        var result = await _service.GetAsync(UserId, Token);

        result.IsSuccess.ShouldBeTrue("the home page must keep rendering when Redis is down");
        result.Value.Degraded.ShouldBeTrue("an empty cart must not look like a real, empty cart");
    }

    [Fact]
    public async Task AddItemAsync_WhenTheStoreIsDown_ReportsUnavailableRatherThanPretendingToHoldIt()
    {
        _rooms.GetByIdAsync(RoomId, Arg.Any<CancellationToken>()).Returns(ADoubleRoom());
        _carts.IsUnreachable = true;

        var result = await _service.AddItemAsync(AStayFor(adults: 2), UserId, Token);

        result.TopError.ShouldBe(CartErrors.Unavailable);
    }
}
