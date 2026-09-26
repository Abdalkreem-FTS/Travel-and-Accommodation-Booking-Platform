using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Cities;
using HotelBooking.Application.Common;
using HotelBooking.Application.Visits;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Cities;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Results;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace HotelBooking.Application.UnitTests.Cities;

public sealed class CityServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid CityId = new("00000000-0000-0000-0002-000000000001");

    private static readonly string IfMatch = ConcurrencyToken.From(new byte[8]).ETag;

    private readonly IHotelRepository _hotels = Substitute.For<IHotelRepository>();
    private readonly IVisitTracker _visits = Substitute.For<IVisitTracker>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly City _city;
    private readonly CityService _service;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public CityServiceTests()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        _city = City.Create(CityId, "Amman", CountryCode.Create("JO").Value, "11118", null, Now).Value;

        var cities = Substitute.For<ICityRepository>();
        cities.GetByIdAsync(CityId, Arg.Any<CancellationToken>()).Returns(_city);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);

        _service = new CityService(
            cities, Substitute.For<ICityQueries>(), _hotels, Substitute.For<IVisitRankings>(), _visits, _unitOfWork,
            Substitute.For<IConcurrencyGuard>(), Substitute.For<IGuidProvider>(), clock,
            NullLogger<CityService>.Instance);
    }

    [Fact]
    public async Task DeleteAsync_WhileHotelsStillStandInIt_IsRefusedAndTheCityStaysTrending()
    {
        _hotels.ExistsInCityAsync(CityId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.DeleteAsync(CityId, IfMatch, Token);

        result.TopError.ShouldBe(CityErrors.HasHotels);
        _city.IsDeleted.ShouldBeFalse();
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(Token);
        await _visits.DidNotReceiveWithAnyArgs().ForgetCityAsync(default, Token);
    }

    [Fact]
    public async Task DeleteAsync_ForAnEmptyCity_DeletesItAndDropsItFromTheTrendingList()
    {
        var result = await _service.DeleteAsync(CityId, IfMatch, Token);

        result.IsSuccess.ShouldBeTrue();
        _city.IsDeleted.ShouldBeTrue();
        await _visits.Received(1).ForgetCityAsync(CityId, Arg.Any<CancellationToken>());
    }
}
