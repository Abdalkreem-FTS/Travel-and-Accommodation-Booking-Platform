using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Amenities;
using HotelBooking.Application.Common;
using HotelBooking.Application.Hotels;
using HotelBooking.Application.Hotels.Dtos;
using HotelBooking.Application.Visits;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Amenities;
using HotelBooking.Domain.Cities;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace HotelBooking.Application.UnitTests.Hotels;

public sealed class HotelServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid HotelId = new("00000000-0000-0000-0003-000000000001");
    private static readonly Guid CityId = new("00000000-0000-0000-0002-000000000001");

    private static readonly string IfMatch = ConcurrencyToken.From(new byte[8]).ETag;

    private readonly IHotelRepository _hotels = Substitute.For<IHotelRepository>();
    private readonly ICityRepository _cities = Substitute.For<ICityRepository>();
    private readonly IRoomRepository _rooms = Substitute.For<IRoomRepository>();
    private readonly IAmenityQueries _amenities = Substitute.For<IAmenityQueries>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly Hotel _hotel;
    private readonly HotelService _service;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public HotelServiceTests()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        var guard = Substitute.For<IConcurrencyGuard>();
        guard.TokenFor(Arg.Any<Hotel>()).Returns(ConcurrencyToken.From(new byte[8]));

        _hotel = Hotel.Create(
            HotelId, CityId, "Grand Plaza", "By the sea.", "Plaza Group",
            StarRating.Create(4).Value, GeoLocation.Create(31.95m, 35.93m).Value, null, Now).Value;

        _hotels.GetByIdAsync(HotelId, Arg.Any<CancellationToken>()).Returns(_hotel);
        _cities.ExistsAsync(CityId, Arg.Any<CancellationToken>()).Returns(true);
        _amenities.ListByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);

        _service = new HotelService(
            _hotels, Substitute.For<IHotelQueries>(), _cities, _rooms, _amenities, _cache, Substitute.For<IVisitTracker>(), _unitOfWork, guard,
            Substitute.For<IGuidProvider>(), clock, NullLogger<HotelService>.Instance);
    }

    private static UpdateHotelRequest ARename(Guid? cityId = null, IReadOnlyList<Guid>? amenityIds = null) =>
        new(cityId ?? CityId, "Grand Plaza Resort", "By the sea.", "Plaza Group", 5, 31.95m, 35.93m, null,
            AmenityIds: amenityIds);

    [Fact]
    public async Task UpdateAsync_DropsTheCachedDetailsSoTheNextReadSeesTheChange()
    {
        var result = await _service.UpdateAsync(HotelId, ARename(), IfMatch, Token);

        result.Value.Name.ShouldBe("Grand Plaza Resort");
        await _cache.Received(1).RemoveAsync(HotelCacheKeys.Details(HotelId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_IntoACityThatDoesNotExist_IsRefusedAndTheHotelKeepsItsCity()
    {
        var result = await _service.UpdateAsync(HotelId, ARename(cityId: Guid.NewGuid()), IfMatch, Token);

        result.TopError.ShouldBe(HotelErrors.CityNotFound);
        _hotel.CityId.ShouldBe(CityId);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(Token);
    }

    [Fact]
    public async Task UpdateAsync_LinkingAnAmenityNoOneKnows_IsRefused()
    {
        var result = await _service.UpdateAsync(HotelId, ARename(amenityIds: [Guid.NewGuid()]), IfMatch, Token);

        result.TopError.ShouldBe(AmenityErrors.UnknownAmenity);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(Token);
    }

    [Fact]
    public async Task DeleteAsync_WhileRoomsStillBelongToIt_IsRefusedAndTheHotelStays()
    {
        _rooms.ExistsInHotelAsync(HotelId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.DeleteAsync(HotelId, IfMatch, Token);

        result.TopError.ShouldBe(HotelErrors.HasRooms);
        _hotel.IsDeleted.ShouldBeFalse();
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(Token);
    }
}
