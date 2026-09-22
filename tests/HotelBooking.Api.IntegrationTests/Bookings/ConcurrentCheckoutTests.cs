using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using HotelBooking.Api.IntegrationTests.Infrastructure;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Cities;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Rooms;
using HotelBooking.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HotelBooking.Api.IntegrationTests.Bookings;

public sealed class ConcurrentCheckoutTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private const int Checkouts = 50;

    private const int Nights = 3;

    private const string Email = "abdalkreem@example.com";

    private const string InventoryRows = "SELECT COUNT(*) AS Value FROM RoomNightInventory";

    private const string BookingRows = "SELECT COUNT(*) AS Value FROM Bookings";

    private static readonly DateTimeOffset Seeded = new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Post_FiftyTimesAtOnceForOneRoom_SellsTheseNightsExactlyOnce(int attempt)
    {
        var roomId = await ARoomAsync();
        var session = await SignUpAndLogInAsync(Email);

        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        var checkOut = checkIn.AddDays(Nights);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, Checkouts).Select(_ => CheckOutAsync(session.AccessToken, roomId, checkIn, checkOut)));

        var statuses = responses.Select(response => response.StatusCode).ToArray();

        statuses.ShouldNotContain(
            HttpStatusCode.InternalServerError,
            $"attempt {attempt} turned a losing race into a server error");

        statuses.Count(status => status == HttpStatusCode.Created)
            .ShouldBe(1, $"attempt {attempt} sold the same room-nights more than once");

        statuses.Count(status => status == HttpStatusCode.Conflict)
            .ShouldBe(Checkouts - 1, $"attempt {attempt} answered a loser with something other than 409");

        foreach (var loser in responses.Where(response => response.StatusCode == HttpStatusCode.Conflict))
        {
            (await ErrorCodeAsync(loser)).ShouldBe(
                "Booking.RoomUnavailable",
                "a loser racing on inventory must not be told its own key is in progress");
        }

        (await CountAsync(InventoryRows)).ShouldBe(Nights, "the ledger must hold one row per sold night");
        (await CountAsync(BookingRows)).ShouldBe(1);

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    private async Task<HttpResponseMessage> CheckOutAsync(
        string accessToken,
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        request.Content = JsonContent.Create(new
        {
            items = new[] { new { roomId, checkIn, checkOut, adults = 2, children = 1 } }
        });

        return await Client.SendAsync(request, Token);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        return document.RootElement.TryGetProperty("errorCode", out var code)
            ? code.GetString()
            : null;
    }

    private async Task<int> CountAsync(string sql)
    {
        using var scope = Factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>();

        return await context.Database.SqlQueryRaw<int>(sql).SingleAsync(Token);
    }

    private async Task<Guid> ARoomAsync()
    {
        using var scope = Factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>();

        var city = City.Create(
            Guid.NewGuid(), "Amman", CountryCode.Create("JO").Value, "11118", null, Seeded).Value;

        var hotel = Hotel.Create(
            Guid.NewGuid(),
            city.Id,
            "Amman Rotana",
            "A tower hotel overlooking the sixth circle.",
            "Rotana Hotel Management",
            StarRating.Create(5).Value,
            GeoLocation.Create(31.963158m, 35.930359m).Value,
            null,
            Seeded).Value;

        var room = Room.Create(
            Guid.NewGuid(),
            hotel.Id,
            "1203",
            RoomType.Luxury,
            Occupancy.Create(2, 1).Value,
            Money.Create(120.50m, "USD").Value,
            Seeded).Value;

        context.Cities.Add(city);
        context.Hotels.Add(hotel);
        context.Rooms.Add(room);

        (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token))
            .IsSuccess.ShouldBeTrue();

        return room.Id;
    }
}
