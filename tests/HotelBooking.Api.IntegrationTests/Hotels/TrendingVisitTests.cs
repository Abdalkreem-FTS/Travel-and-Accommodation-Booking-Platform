using System.Net.Http.Json;
using System.Text.Json;

using HotelBooking.Api.IntegrationTests.Infrastructure;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Cities;
using HotelBooking.Domain.Common;
using HotelBooking.Infrastructure.Caching;
using HotelBooking.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace HotelBooking.Api.IntegrationTests.Hotels;

public sealed class TrendingVisitTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_AHotel_CountsEachVisitorOncePerCityPerDay()
    {
        var cityId = await ACityAsync();
        var admin = await SignUpAndLogInAsAdminAsync("abdalkreem@example.com");
        var grandPlaza = await AHotelAsync(admin.AccessToken, cityId, "Grand Plaza");
        var sixthCircle = await AHotelAsync(admin.AccessToken, cityId, "Sixth Circle Suites");

        var omar = await SignUpAndLogInAsync("omar@example.com");
        var khaled = await SignUpAndLogInAsync("khaled@example.com");

        foreach (var (hotelId, accessToken) in new[]
        {
            (grandPlaza, omar.AccessToken),
            (grandPlaza, omar.AccessToken),
            (sixthCircle, omar.AccessToken),
            (grandPlaza, khaled.AccessToken),
        })
        {
            using var view = await SendAsync(HttpMethod.Get, $"/api/hotels/{hotelId}", accessToken);
            view.EnsureSuccessStatusCode();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var score = await Factory.Cache.SortedSetScoreAsync(
            RedisVisitStore.TrendingDayKey(today), RedisVisitStore.Member(cityId));

        score.ShouldBe(2, "Omar's three views across two of the city's hotels are one visit, Khaled's is the other");
    }

    private async Task<Guid> AHotelAsync(string accessToken, Guid cityId, string name)
    {
        using var created = await SendAsync(
            HttpMethod.Post,
            "/api/hotels",
            accessToken,
            JsonContent.Create(new
            {
                cityId,
                name,
                description = "A tower hotel overlooking the sixth circle.",
                owner = "Rotana Hotel Management",
                starRating = 5,
                latitude = 31.963158m,
                longitude = 35.930359m,
            }));

        created.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Token));

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> ACityAsync()
    {
        using var scope = Factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>();

        var city = City.Create(
            Guid.NewGuid(), "Amman", CountryCode.Create("JO").Value, "11118", null, DateTimeOffset.UtcNow).Value;

        context.Cities.Add(city);

        (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token))
            .IsSuccess.ShouldBeTrue();

        return city.Id;
    }
}
