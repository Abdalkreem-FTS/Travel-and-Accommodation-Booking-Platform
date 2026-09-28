using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Common;
using HotelBooking.Application.Deals;
using HotelBooking.Application.Deals.Dtos;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Deals;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace HotelBooking.Application.UnitTests.Deals;

public sealed class DealServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Start = new(2026, 10, 1);

    private static readonly Guid RoomId = new("00000000-0000-0000-0004-000000000001");
    private static readonly Guid HotelId = new("00000000-0000-0000-0003-000000000001");

    private readonly IDealRepository _deals = Substitute.For<IDealRepository>();
    private readonly IRoomRepository _rooms = Substitute.For<IRoomRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DealService _service;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public DealServiceTests()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        var guids = Substitute.For<IGuidProvider>();
        guids.NewSortable().Returns(_ => Guid.NewGuid());

        var guard = Substitute.For<IConcurrencyGuard>();
        guard.TokenFor(Arg.Any<Deal>()).Returns(ConcurrencyToken.From(new byte[8]));

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);

        _rooms.GetByIdAsync(RoomId, Arg.Any<CancellationToken>()).Returns(
            Room.Create(
                RoomId, HotelId, "1203", RoomType.Luxury, Occupancy.Create(2, 0).Value,
                Money.Create(100m, "USD").Value, Now).Value);

        _service = new DealService(
            Substitute.For<IDealQueries>(), _deals, _rooms, _cache, _unitOfWork, guard, guids, clock,
            NullLogger<DealService>.Instance);
    }

    private static CreateDealRequest ADeal(Guid? roomId = null) =>
        new(roomId ?? RoomId, 20, Start, Start.AddDays(3), IsFeatured: true);

    [Fact]
    public async Task CreateAsync_DropsEveryCachedFeaturedListSoTheHomePageShowsTheNewDeal()
    {
        var result = await _service.CreateAsync(ADeal(), Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HotelId.ShouldBe(HotelId, "the hotel comes from the room, not the caller");

        foreach (var key in DealCacheKeys.AllFeatured())
        {
            await _cache.Received(1).RemoveAsync(key, Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task CreateAsync_WhenTheDatabaseRefusesAnOverlap_ReportsItAndLeavesTheCachedListsAlone()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(DealErrors.OverlapsExisting);

        var result = await _service.CreateAsync(ADeal(), Token);

        result.TopError.ShouldBe(DealErrors.OverlapsExisting);
        await _cache.DidNotReceiveWithAnyArgs().RemoveAsync(default, Token);
    }

    [Fact]
    public async Task CreateAsync_ForARoomThatDoesNotExist_IsRefusedBeforeAnythingIsWritten()
    {
        var result = await _service.CreateAsync(ADeal(roomId: Guid.NewGuid()), Token);

        result.TopError.ShouldBe(DealErrors.RoomNotFound);
        _deals.DidNotReceiveWithAnyArgs().Add(null!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(Token);
    }
}
