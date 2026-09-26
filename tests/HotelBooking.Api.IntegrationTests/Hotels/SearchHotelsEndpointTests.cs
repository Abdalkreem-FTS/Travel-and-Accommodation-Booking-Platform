using System.Globalization;
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

namespace HotelBooking.Api.IntegrationTests.Hotels;

public sealed class SearchHotelsEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset Seeded = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_AskingForMoreThanTheMaximumPage_Is400NamingThePageSize()
    {
        using var response = await Client.GetAsync("/api/hotels?pageSize=500", Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        body.RootElement.GetProperty("errors").TryGetProperty("pageSize", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Get_ForNightsTheOnlyRoomIsAlreadySoldFor_LeavesThatHotelOut()
    {
        var cityId = Guid.NewGuid();
        var (_, soldRoomId) = await AHotelWithOneRoomAsync(cityId, "Amman Rotana", seedCity: true);
        var (freeHotelId, _) = await AHotelWithOneRoomAsync(cityId, "Le Royal");

        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        var guest = await SignUpAndLogInAsync("abdalkreem@example.com");

        using var booked = await CheckOutAsync(guest.AccessToken, soldRoomId, checkIn, checkIn.AddDays(2));
        booked.StatusCode.ShouldBe(HttpStatusCode.Created);

        var overlapping = await SearchAsync(cityId, checkIn.AddDays(1), checkIn.AddDays(3));
        var afterwards = await SearchAsync(cityId, checkIn.AddDays(2), checkIn.AddDays(4));

        overlapping.ShouldBe([freeHotelId], "a hotel with no room free on one of those nights cannot be booked");
        afterwards.Count.ShouldBe(2, "the checkout night is not sold");
    }

    [Fact]
    public async Task Get_NeverListsADeletedHotel()
    {
        var cityId = Guid.NewGuid();
        var (deletedHotelId, _) = await AHotelWithOneRoomAsync(cityId, "Amman Rotana", seedCity: true);
        var (liveHotelId, _) = await AHotelWithOneRoomAsync(cityId, "Le Royal");

        using (var scope = Factory.Services.CreateScope())
        {
            var hotel = await scope.ServiceProvider.GetRequiredService<IHotelRepository>()
                .GetByIdAsync(deletedHotelId, Token);
            hotel!.Delete(Seeded).IsSuccess.ShouldBeTrue();

            (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token))
                .IsSuccess.ShouldBeTrue();
        }

        (await SearchAsync(cityId)).ShouldBe([liveHotelId]);
    }

    private async Task<List<Guid>> SearchAsync(Guid cityId, DateOnly? checkIn = null, DateOnly? checkOut = null)
    {
        var route = $"/api/hotels?cityId={cityId}&adults=2&children=0";

        if (checkIn is { } from && checkOut is { } to)
        {
            route += string.Create(CultureInfo.InvariantCulture, $"&checkIn={from:yyyy-MM-dd}&checkOut={to:yyyy-MM-dd}");
        }

        using var response = await Client.GetAsync(route, Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        return [.. body.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
    }

    private async Task<HttpResponseMessage> CheckOutAsync(string accessToken, Guid roomId, DateOnly checkIn, DateOnly checkOut)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new
        {
            items = new[] { new { roomId, checkIn, checkOut, adults = 2, children = 0 } }
        });

        return await Client.SendAsync(request, Token);
    }

    private async Task<(Guid HotelId, Guid RoomId)> AHotelWithOneRoomAsync(Guid cityId, string name, bool seedCity = false)
    {
        using var scope = Factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>();

        if (seedCity)
        {
            context.Cities.Add(
                City.Create(cityId, "Amman", CountryCode.Create("JO").Value, "11118", null, Seeded).Value);
        }

        var hotel = Hotel.Create(
            Guid.NewGuid(), cityId, name, "In the heart of Amman.", "Amman Hospitality",
            StarRating.Create(5).Value, GeoLocation.Create(31.963158m, 35.930359m).Value, null, Seeded).Value;

        var room = Room.Create(
            Guid.NewGuid(), hotel.Id, "101", RoomType.Standard, Occupancy.Create(2, 0).Value,
            Money.Create(100m, "USD").Value, Seeded).Value;

        context.Hotels.Add(hotel);
        context.Rooms.Add(room);

        (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token))
            .IsSuccess.ShouldBeTrue();

        return (hotel.Id, room.Id);
    }
}
