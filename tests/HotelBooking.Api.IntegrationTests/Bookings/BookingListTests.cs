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

public sealed class BookingListTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private const string Email = "abdalkreem@example.com";

    private const string OtherGuestEmail = "omar@example.com";

    private static readonly DateTimeOffset Seeded = new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly CheckIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_ListsOnlyTheCallersBookingsNewestFirst_NamingAHotelDeletedSince()
    {
        var (rotanaId, rotanaRooms) = await AHotelAsync("Amman Rotana", "Amman", rooms: 2);
        var (_, petraRooms) = await AHotelAsync("Grand Petra Hotel", "Petra", rooms: 2);

        var guest = await SignUpAndLogInAsync(Email);
        var otherGuest = await SignUpAndLogInAsync(OtherGuestEmail);

        using var older = await CheckOutAsync(guest.AccessToken, rotanaRooms[0]);
        older.StatusCode.ShouldBe(HttpStatusCode.Created);
        await ExecuteAsync(db => db.Database.ExecuteSqlAsync(
            $"UPDATE Bookings SET Status = 5 WHERE UserId = (SELECT Id FROM Users WHERE Email = {Email})", Token));

        using var newer = await CheckOutAsync(guest.AccessToken, petraRooms[0], petraRooms[1]);
        newer.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var somebodyElses = await CheckOutAsync(otherGuest.AccessToken, rotanaRooms[1]);
        somebodyElses.StatusCode.ShouldBe(HttpStatusCode.Created);

        await ExecuteAsync(db => db.Database.ExecuteSqlAsync(
            $"UPDATE Hotels SET IsDeleted = 1 WHERE Id = {rotanaId}", Token));

        using var response = await SendAsync(HttpMethod.Get, "/api/bookings", guest.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();

        body.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(2, "the other guest's booking is not ours");
        items.Select(item => item.GetProperty("hotelName").GetString())
            .ShouldBe(["Grand Petra Hotel", "Amman Rotana"], "newest first, and a deleted hotel keeps its name");
        items.Select(item => item.GetProperty("rooms").GetInt32()).ShouldBe([2, 1]);
        items.Select(item => item.GetProperty("status").GetString()).ShouldBe(["Pending", "Expired"]);
    }

    [Fact]
    public async Task Get_WithAPageSizeOverTheMaximum_IsAValidationProblem()
    {
        var guest = await SignUpAndLogInAsync(Email);

        using var response = await SendAsync(HttpMethod.Get, "/api/bookings?pageSize=51", guest.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        body.RootElement.GetProperty("errors").TryGetProperty("pageSize", out _).ShouldBeTrue();
    }

    private async Task<HttpResponseMessage> CheckOutAsync(string accessToken, params Guid[] roomIds)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        request.Content = JsonContent.Create(new
        {
            items = roomIds.Select(roomId => new
            {
                roomId,
                checkIn = CheckIn,
                checkOut = CheckIn.AddDays(2),
                adults = 2,
                children = 0
            })
        });

        return await Client.SendAsync(request, Token);
    }

    private async Task ExecuteAsync(Func<HotelBookingDbContext, Task<int>> statement)
    {
        using var scope = Factory.Services.CreateScope();

        await statement(scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>());
    }

    private async Task<(Guid HotelId, Guid[] RoomIds)> AHotelAsync(string name, string cityName, int rooms)
    {
        using var scope = Factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>();

        var city = City.Create(
            Guid.NewGuid(), cityName, CountryCode.Create("JO").Value, "11118", null, Seeded).Value;

        var hotel = Hotel.Create(
            Guid.NewGuid(),
            city.Id,
            name,
            "A hotel for the booking list.",
            "Hotel Management",
            StarRating.Create(5).Value,
            GeoLocation.Create(31.963158m, 35.930359m).Value,
            null,
            Seeded).Value;

        var hotelRooms = Enumerable.Range(1, rooms)
            .Select(number => Room.Create(
                Guid.NewGuid(),
                hotel.Id,
                $"10{number}",
                RoomType.Luxury,
                Occupancy.Create(2, 1).Value,
                Money.Create(120.50m, "USD").Value,
                Seeded).Value)
            .ToArray();

        context.Cities.Add(city);
        context.Hotels.Add(hotel);
        context.Rooms.AddRange(hotelRooms);

        (await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token))
            .IsSuccess.ShouldBeTrue();

        return (hotel.Id, [.. hotelRooms.Select(room => room.Id)]);
    }
}
