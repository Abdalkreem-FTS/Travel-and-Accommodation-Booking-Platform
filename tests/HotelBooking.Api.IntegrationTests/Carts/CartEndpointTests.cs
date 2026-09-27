using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using HotelBooking.Api.IntegrationTests.Infrastructure;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Cities;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Rooms;
using HotelBooking.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace HotelBooking.Api.IntegrationTests.Carts;

public sealed class CartEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset Seeded = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PostThenGetThenDelete_HoldsTheStayInRedisAndReleasesIt()
    {
        var roomId = await ARoomAsync();
        var guest = await SignUpAndLogInAsync("abdalkreem@example.com");
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);

        using var added = await AddAsync(guest.AccessToken, roomId, checkIn);
        added.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var held = await CartAsync(guest.AccessToken);
        var item = held.RootElement.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        item.GetProperty("roomId").GetGuid().ShouldBe(roomId);
        item.GetProperty("total").GetDecimal().ShouldBe(300m, "three nights at 100");

        using var removed = await SendAsync(
            HttpMethod.Delete, $"/api/cart/items/{item.GetProperty("id").GetString()}", guest.AccessToken);
        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var emptied = await CartAsync(guest.AccessToken);
        emptied.RootElement.GetProperty("itemCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Get_ByAnotherGuest_NeverSeesOrRemovesTheFirstGuestsStay()
    {
        var roomId = await ARoomAsync();
        var abdalkreem = await SignUpAndLogInAsync("abdalkreem@example.com");
        var omar = await SignUpAndLogInAsync("omar@example.com");

        using var added = await AddAsync(abdalkreem.AccessToken, roomId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10));
        var itemId = (await CartAsync(abdalkreem.AccessToken)).RootElement
            .GetProperty("items")[0].GetProperty("id").GetString();

        using var theirs = await CartAsync(omar.AccessToken);
        using var removed = await SendAsync(HttpMethod.Delete, $"/api/cart/items/{itemId}", omar.AccessToken);
        using var still = await CartAsync(abdalkreem.AccessToken);

        theirs.RootElement.GetProperty("itemCount").GetInt32().ShouldBe(0);
        removed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        still.RootElement.GetProperty("itemCount").GetInt32().ShouldBe(1);
    }

    private Task<HttpResponseMessage> AddAsync(string accessToken, Guid roomId, DateOnly checkIn) =>
        SendAsync(
            HttpMethod.Post,
            "/api/cart/items",
            accessToken,
            JsonContent.Create(new { roomId, checkIn, checkOut = checkIn.AddDays(3), adults = 2, children = 0 }));

    private async Task<JsonDocument> CartAsync(string accessToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/cart", accessToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
    }

    private async Task<Guid> ARoomAsync()
    {
        using var scope = Factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>();

        var city = City.Create(Guid.NewGuid(), "Amman", CountryCode.Create("JO").Value, "11118", null, Seeded).Value;

        var hotel = Hotel.Create(
            Guid.NewGuid(), city.Id, "Amman Rotana", "In the heart of Amman.", "Rotana Hotel Management",
            StarRating.Create(5).Value, GeoLocation.Create(31.963158m, 35.930359m).Value, null, Seeded).Value;

        var room = Room.Create(
            Guid.NewGuid(), hotel.Id, "101", RoomType.Standard, Occupancy.Create(2, 0).Value,
            Money.Create(100m, "USD").Value, Seeded).Value;

        context.Cities.Add(city);
        context.Hotels.Add(hotel);
        context.Rooms.Add(room);

        (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token))
            .IsSuccess.ShouldBeTrue();

        return room.Id;
    }
}
