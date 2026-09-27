using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using HotelBooking.Api.IntegrationTests.Infrastructure;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Cities;
using HotelBooking.Domain.Common;
using HotelBooking.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HotelBooking.Api.IntegrationTests.Catalogue;

public sealed class CatalogueEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private const string AdminEmail = "abdalkreem@example.com";

    private static readonly DateTimeOffset Seeded = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_AHotelAsAGuest_Is403AndAddsNothing()
    {
        var cityId = await ACityAsync();
        var guest = await SignUpAndLogInAsync("omar@example.com");

        using var response = await SendAsync(
            HttpMethod.Post, "/api/hotels", guest.AccessToken, JsonContent.Create(AHotel(cityId, "Grand Plaza")));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CountAsync("SELECT COUNT(*) AS Value FROM Hotels")).ShouldBe(0);
    }

    [Fact]
    public async Task Put_WithTheVersionAnotherAdminAlreadyOverwrote_Is409AndTheFirstWriteSurvives()
    {
        var cityId = await ACityAsync();
        var admin = await SignUpAndLogInAsAdminAsync(AdminEmail);

        using var created = await SendAsync(
            HttpMethod.Post, "/api/hotels", admin.AccessToken, JsonContent.Create(AHotel(cityId, "Grand Plaza")));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var hotelId = await IdAsync(created);
        var readVersion = created.Headers.ETag!.ToString();

        using var first = await PutHotelAsync(admin.AccessToken, hotelId, AHotel(cityId, "First Edit"), readVersion);
        using var second = await PutHotelAsync(admin.AccessToken, hotelId, AHotel(cityId, "Second Edit"), readVersion);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(second)).ShouldBe("Persistence.ConcurrencyConflict");

        using var current = await SendAsync(HttpMethod.Get, $"/api/hotels/{hotelId}", admin.AccessToken);
        using var body = JsonDocument.Parse(await current.Content.ReadAsStringAsync(Token));
        body.RootElement.GetProperty("name").GetString().ShouldBe("First Edit", "a stale write must not win");
    }

    [Fact]
    public async Task Post_ARoomNumberTheHotelAlreadyUses_Is409AndNeverA500()
    {
        var cityId = await ACityAsync();
        var admin = await SignUpAndLogInAsAdminAsync(AdminEmail);

        using var hotel = await SendAsync(
            HttpMethod.Post, "/api/hotels", admin.AccessToken, JsonContent.Create(AHotel(cityId, "Grand Plaza")));
        var hotelId = await IdAsync(hotel);

        using var first = await PostRoomAsync(admin.AccessToken, hotelId, "101");
        using var duplicate = await PostRoomAsync(admin.AccessToken, hotelId, "101");

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(duplicate)).ShouldBe("Room.NumberAlreadyUsedInHotel");
    }

    [Fact]
    public async Task Post_ADealOnANightAnotherDealAlreadyDiscounts_Is409AndKeepsOneDealPerNight()
    {
        var cityId = await ACityAsync();
        var admin = await SignUpAndLogInAsAdminAsync(AdminEmail);

        using var hotel = await SendAsync(
            HttpMethod.Post, "/api/hotels", admin.AccessToken, JsonContent.Create(AHotel(cityId, "Grand Plaza")));
        using var room = await PostRoomAsync(admin.AccessToken, await IdAsync(hotel), "101");
        var roomId = await IdAsync(room);

        var startsOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);

        using var first = await PostDealAsync(admin.AccessToken, roomId, startsOn, startsOn.AddDays(5));
        using var overlapping = await PostDealAsync(admin.AccessToken, roomId, startsOn.AddDays(4), startsOn.AddDays(8));
        using var adjacent = await PostDealAsync(admin.AccessToken, roomId, startsOn.AddDays(5), startsOn.AddDays(8));

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        overlapping.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(overlapping)).ShouldBe("Deal.OverlapsExisting");
        adjacent.StatusCode.ShouldBe(HttpStatusCode.Created, "a deal may start on the night the last one ended");
    }

    [Fact]
    public async Task Get_ACitysHotels_ListsHotelsWithNoRoomsAndLeavesOutRemovedOnesAndOtherCities()
    {
        var cityId = await ACityAsync();
        var otherCityId = await ACityAsync("Petra");
        var admin = await SignUpAndLogInAsAdminAsync(AdminEmail);

        using var withRooms = await SendAsync(
            HttpMethod.Post, "/api/hotels", admin.AccessToken, JsonContent.Create(AHotel(cityId, "Grand Plaza")));
        using var room101 = await PostRoomAsync(admin.AccessToken, await IdAsync(withRooms), "101");
        using var room102 = await PostRoomAsync(admin.AccessToken, await IdAsync(withRooms), "102");

        using var noRooms = await SendAsync(
            HttpMethod.Post, "/api/hotels", admin.AccessToken, JsonContent.Create(AHotel(cityId, "Amman Rotana")));

        using var removed = await SendAsync(
            HttpMethod.Post, "/api/hotels", admin.AccessToken, JsonContent.Create(AHotel(cityId, "Crowne Plaza")));
        using var deletion = await DeleteAsync(
            admin.AccessToken, $"/api/hotels/{await IdAsync(removed)}", removed.Headers.ETag!.ToString());
        deletion.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var elsewhere = await SendAsync(
            HttpMethod.Post, "/api/hotels", admin.AccessToken, JsonContent.Create(AHotel(otherCityId, "Petra Plaza")));

        using var all = await SendAsync(HttpMethod.Get, $"/api/cities/{cityId}/hotels", admin.AccessToken);
        using var searched = await SendAsync(
            HttpMethod.Get, $"/api/cities/{cityId}/hotels?search=PLAZA", admin.AccessToken);

        all.StatusCode.ShouldBe(HttpStatusCode.OK);

        var listed = await ItemsAsync(all);
        listed.Select(hotel => hotel.GetProperty("name").GetString())
            .ShouldBe(["Amman Rotana", "Grand Plaza"], "by name; the removed hotel and the other city's are left out");
        listed.Select(hotel => hotel.GetProperty("roomCount").GetInt32())
            .ShouldBe([0, 2], "a hotel with no rooms is listed, which guest search never does");

        (await ItemsAsync(searched)).Select(hotel => hotel.GetProperty("name").GetString())
            .ShouldBe(["Grand Plaza"], "search ignores case");
    }

    [Fact]
    public async Task Get_TheHotelsOfAnUnknownCity_Is404()
    {
        var admin = await SignUpAndLogInAsAdminAsync(AdminEmail);

        using var response = await SendAsync(HttpMethod.Get, $"/api/cities/{Guid.NewGuid()}/hotels", admin.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ErrorCodeAsync(response)).ShouldBe("City.NotFound");
    }

    [Fact]
    public async Task Get_ARoomsDeals_ListsUpcomingAndUnfeaturedOnesWithAVersionThatDeletes()
    {
        var cityId = await ACityAsync();
        var admin = await SignUpAndLogInAsAdminAsync(AdminEmail);

        using var hotel = await SendAsync(
            HttpMethod.Post, "/api/hotels", admin.AccessToken, JsonContent.Create(AHotel(cityId, "Grand Plaza")));
        var hotelId = await IdAsync(hotel);
        using var room = await PostRoomAsync(admin.AccessToken, hotelId, "101");
        using var otherRoom = await PostRoomAsync(admin.AccessToken, hotelId, "102");
        var roomId = await IdAsync(room);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var unfeatured = await PostDealAsync(admin.AccessToken, roomId, today.AddDays(10), today.AddDays(15));
        using var featured = await PostDealAsync(
            admin.AccessToken, roomId, today.AddDays(20), today.AddDays(22), isFeatured: true);
        using var latest = await PostDealAsync(admin.AccessToken, roomId, today.AddDays(30), today.AddDays(32));
        using var elsewhere = await PostDealAsync(
            admin.AccessToken, await IdAsync(otherRoom), today.AddDays(10), today.AddDays(15));

        using var before = await SendAsync(HttpMethod.Get, $"/api/rooms/{roomId}/deals", admin.AccessToken);

        before.StatusCode.ShouldBe(HttpStatusCode.OK);

        var listed = await ItemsAsync(before);
        listed.Select(deal => deal.GetProperty("id").GetGuid()).ShouldBe(
            [await IdAsync(latest), await IdAsync(featured), await IdAsync(unfeatured)],
            "latest start first, none of them running today, and not the other room's");

        using var deletion = await DeleteAsync(
            admin.AccessToken, $"/api/deals/{listed[0].GetProperty("id").GetGuid()}",
            listed[0].GetProperty("version").GetString()!);

        deletion.StatusCode.ShouldBe(HttpStatusCode.NoContent, "the listed version is the one a delete must quote");

        using var after = await SendAsync(HttpMethod.Get, $"/api/rooms/{roomId}/deals", admin.AccessToken);

        (await ItemsAsync(after)).Select(deal => deal.GetProperty("id").GetGuid())
            .ShouldBe([await IdAsync(featured), await IdAsync(unfeatured)], "a removed deal is left out");
    }

    [Fact]
    public async Task Get_TheDealsOfAnUnknownRoom_Is404()
    {
        var admin = await SignUpAndLogInAsAdminAsync(AdminEmail);

        using var response = await SendAsync(HttpMethod.Get, $"/api/rooms/{Guid.NewGuid()}/deals", admin.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ErrorCodeAsync(response)).ShouldBe("Room.NotFound");
    }

    private static object AHotel(Guid cityId, string name) => new
    {
        cityId,
        name,
        description = "A tower hotel overlooking the sixth circle.",
        owner = "Rotana Hotel Management",
        starRating = 5,
        latitude = 31.963158m,
        longitude = 35.930359m,
    };

    private async Task<HttpResponseMessage> PutHotelAsync(string accessToken, Guid hotelId, object body, string ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/hotels/{hotelId}");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        request.Content = JsonContent.Create(body);

        return await Client.SendAsync(request, Token);
    }

    private async Task<HttpResponseMessage> DeleteAsync(string accessToken, string route, string ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, route);
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);

        return await Client.SendAsync(request, Token);
    }

    private Task<HttpResponseMessage> PostRoomAsync(string accessToken, Guid hotelId, string number) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/hotels/{hotelId}/rooms",
            accessToken,
            JsonContent.Create(new { number, type = 0, adults = 2, children = 0, basePrice = 100m, currency = "USD" }));

    private Task<HttpResponseMessage> PostDealAsync(
        string accessToken, Guid roomId, DateOnly startsOn, DateOnly endsOn, bool isFeatured = false) =>
        SendAsync(
            HttpMethod.Post,
            "/api/deals",
            accessToken,
            JsonContent.Create(new { roomId, discountPercentage = 20, startsOn, endsOn, isFeatured }));

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<List<JsonElement>> ItemsAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        return [.. document.RootElement.GetProperty("items").EnumerateArray().Select(item => item.Clone())];
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        return document.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    private async Task<int> CountAsync(string sql)
    {
        using var scope = Factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>();

        return await context.Database.SqlQueryRaw<int>(sql).SingleAsync(Token);
    }

    private async Task<Guid> ACityAsync(string name = "Amman")
    {
        using var scope = Factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>();

        var city = City.Create(Guid.NewGuid(), name, CountryCode.Create("JO").Value, "11118", null, Seeded).Value;

        context.Cities.Add(city);

        (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token))
            .IsSuccess.ShouldBeTrue();

        return city.Id;
    }
}
