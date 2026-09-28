using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using HotelBooking.Api.IntegrationTests.Infrastructure;
using HotelBooking.Application.Bookings;
using HotelBooking.Application.Payments;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Cities;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Rooms;
using HotelBooking.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HotelBooking.Api.IntegrationTests.Payments;

public sealed class PaymentTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private const int Nights = 3;

    private const string Email = "abdalkreem@example.com";

    private static readonly DateTimeOffset Seeded = new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed record Checkout(Guid BookingId, string CheckoutId, decimal Amount, string Currency);

    [Fact]
    public async Task Post_ACompletedCheckoutDeliveredTwice_ConfirmsTheBookingAndQueuesOneEmail()
    {
        var session = await SignUpAndLogInAsync(Email);
        var checkout = await CheckOutAsync(session.AccessToken, await ARoomAsync());

        var completed = CompletedEvent("evt_completed", checkout);

        using var first = await DeliverAsync(completed);
        using var redelivered = await DeliverAsync(completed);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        redelivered.StatusCode.ShouldBe(HttpStatusCode.OK, "a redelivery must not make the provider retry forever");

        (await BookingStatusAsync(checkout)).ShouldBe(1);
        (await ScalarAsync("SELECT COUNT(*) AS Value FROM OutboxMessages WHERE Type LIKE '%BookingConfirmed%'"))
            .ShouldBe(1, "the guest gets one confirmation email however often the provider knocks");
    }

    [Fact]
    public async Task Post_AnExpiryArrivingAfterThePayment_LeavesThePaidBookingAndItsNightsAlone()
    {
        var session = await SignUpAndLogInAsync(Email);
        var checkout = await CheckOutAsync(session.AccessToken, await ARoomAsync());

        using var paid = await DeliverAsync(CompletedEvent("evt_completed", checkout));
        using var late = await DeliverAsync(ExpiredEvent("evt_expired", checkout));

        late.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await BookingStatusAsync(checkout)).ShouldBe(1);
        (await ScalarAsync("SELECT COUNT(*) AS Value FROM RoomNightInventory")).ShouldBe(Nights);
    }

    [Fact]
    public async Task Post_AnExpiredCheckout_ReleasesTheNightsAndLetsTheGuestBookAgain()
    {
        var session = await SignUpAndLogInAsync(Email);
        var roomId = await ARoomAsync();
        var checkout = await CheckOutAsync(session.AccessToken, roomId);

        using var expired = await DeliverAsync(ExpiredEvent("evt_expired", checkout));

        expired.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ScalarAsync("SELECT COUNT(*) AS Value FROM RoomNightInventory")).ShouldBe(0);

        var again = await CheckOutAsync(session.AccessToken, roomId);

        again.BookingId.ShouldNotBe(checkout.BookingId, "the same nights are on sale again, to the same guest");
    }

    [Fact]
    public async Task ExpireOverdue_ReleasesTheNightsOfAnOverdueCheckoutAndLeavesOneStillInTimeAlone()
    {
        var roomId = await ARoomAsync();
        var overdue = await CheckOutAsync((await SignUpAndLogInAsync(Email)).AccessToken, roomId);
        var inTime = await CheckOutAsync((await SignUpAndLogInAsync("omar@example.com")).AccessToken, roomId, startingIn: 40);

        await MakeOverdueAsync(overdue);
        await ExpireOverdueAsync();

        (await BookingStatusAsync(overdue)).ShouldBe(5, "an overdue booking is expired");
        (await BookingStatusAsync(inTime)).ShouldBe(0, "a guest still inside the hold keeps it");
        (await ScalarAsync("SELECT COUNT(*) AS Value FROM RoomNightInventory")).ShouldBe(Nights);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ExpireOverdue_RacingThePaymentEvent_NeverKeepsMoneyForNightsTheGuestDoesNotHold(int attempt)
    {
        const int checkouts = 5;

        var roomId = await ARoomAsync();

        for (var guest = 0; guest < checkouts; guest++)
        {
            var session = await SignUpAndLogInAsync($"guest-{guest}@example.com");
            var checkout = await CheckOutAsync(session.AccessToken, roomId, startingIn: 30 + (guest * 5));

            await MakeOverdueAsync(checkout);

            var delivery = DeliverAsync(CompletedEvent($"evt_{guest}", checkout));
            await Task.WhenAll(delivery, ExpireOverdueAsync());
            (await delivery).Dispose();

            var status = await BookingStatusAsync(checkout);
            var paymentStatus = await PaymentStatusAsync(checkout);
            var nights = await ScalarAsync(
                $"SELECT COUNT(*) AS Value FROM RoomNightInventory WHERE BookingId = '{checkout.BookingId}'");
            (status, paymentStatus, nights).ShouldBeOneOf(
                [(1, 1, Nights), (5, 2, 0), (5, 3, 0)],
                $"attempt {attempt}, guest {guest}: money is never kept for nights the guest does not hold");
        }
    }

    [Fact]
    public async Task Cancel_APaidBooking_ReturnsTheNightsAndQueuesTheRefundAndTheEmail()
    {
        var session = await SignUpAndLogInAsync(Email);
        var checkout = await CheckOutAsync(session.AccessToken, await ARoomAsync());

        using var paid = await DeliverAsync(CompletedEvent("evt_completed", checkout));
        using var cancelled = await SendAsync(
            HttpMethod.Post, $"/api/bookings/{checkout.BookingId}/cancellation", session.AccessToken);

        cancelled.StatusCode.ShouldBe(HttpStatusCode.Created);

        var cancellation = await cancelled.Content.ReadFromJsonAsync<JsonElement>(Token);

        cancellation.GetProperty("refund").GetProperty("amount").GetDecimal().ShouldBe(checkout.Amount);
        (await ScalarAsync("SELECT COUNT(*) AS Value FROM RoomNightInventory")).ShouldBe(0);
        (await PaymentStatusAsync(checkout)).ShouldBe(3, "the refunding payment row is the durable record the unfinished payments worker sends");
        (await ScalarAsync("SELECT COUNT(*) AS Value FROM OutboxMessages WHERE Type LIKE '%BookingCancelled%'"))
            .ShouldBe(1, "and so does the email telling the guest");

        using var scope = Factory.Services.CreateScope();

        var notice = await scope.ServiceProvider.GetRequiredService<IBookingQueries>()
            .GetCancellationNoticeAsync(checkout.BookingId, Token);

        notice.ShouldNotBeNull().GuestEmail.ShouldBe(Email);
        notice.RefundAmount.ShouldBe(checkout.Amount, "the email tells the guest how much is coming back");
    }

    [Fact]
    public async Task SendPending_SendsEveryPendingRefundOnceAndThenHasNothingLeftToSend()
    {
        var roomId = await ARoomAsync();
        var first = await PaidAndCancelledAsync(Email, roomId, startingIn: 30);
        var second = await PaidAndCancelledAsync("omar@example.com", roomId, startingIn: 40);

        using var scope = Factory.Services.CreateScope();
        var refunds = scope.ServiceProvider.GetRequiredService<IPaymentRefundService>();

        (await refunds.SendPendingAsync(50, Token)).ShouldBe(2);

        (await PaymentStatusAsync(first)).ShouldBe(4, "sent and settled");
        (await PaymentStatusAsync(second)).ShouldBe(4);
        (await refunds.SendPendingAsync(50, Token)).ShouldBe(0, "a refund that has ended is never sent again");
    }

    [Fact]
    public async Task Post_WithoutTheProvidersSignature_IsRefusedAndConfirmsNothing()
    {
        var session = await SignUpAndLogInAsync(Email);
        var checkout = await CheckOutAsync(session.AccessToken, await ARoomAsync());

        using var forged = await DeliverAsync(CompletedEvent("evt_forged", checkout), signature: "0000");

        forged.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await BookingStatusAsync(checkout)).ShouldBe(0, "anyone can reach this route, so an unsigned event must never confirm a booking");
    }

    private async Task<Checkout> PaidAndCancelledAsync(string email, Guid roomId, int startingIn)
    {
        var session = await SignUpAndLogInAsync(email);
        var checkout = await CheckOutAsync(session.AccessToken, roomId, startingIn);

        using var paid = await DeliverAsync(CompletedEvent($"evt_{checkout.BookingId:N}", checkout));
        using var cancelled = await SendAsync(
            HttpMethod.Post, $"/api/bookings/{checkout.BookingId}/cancellation", session.AccessToken);

        cancelled.StatusCode.ShouldBe(HttpStatusCode.Created);

        return checkout;
    }

    private Task<int> PaymentStatusAsync(Checkout checkout) =>
        ScalarAsync($"SELECT Status AS Value FROM Payments WHERE BookingId = '{checkout.BookingId}'");

    private Task<int> BookingStatusAsync(Checkout checkout) =>
        ScalarAsync($"SELECT Status AS Value FROM Bookings WHERE Id = '{checkout.BookingId}'");

    private async Task MakeOverdueAsync(Checkout checkout)
    {
        using var scope = Factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>();

        await context.Database.ExecuteSqlAsync(
            $"UPDATE Payments SET ExpiresAtUtc = DATEADD(minute, -1, SYSDATETIMEOFFSET()) WHERE BookingId = {checkout.BookingId}",
            Token);
    }

    private async Task ExpireOverdueAsync()
    {
        using var scope = Factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IPaymentService>().ExpireOverdueAsync(50, Token);
    }

    private static string CompletedEvent(string id, Checkout checkout) =>
        JsonSerializer.Serialize(new
        {
            id,
            type = "checkout.completed",
            checkoutId = checkout.CheckoutId,
            paymentId = $"pi_{id}",
            amount = checkout.Amount,
            currency = checkout.Currency
        });

    private static string ExpiredEvent(string id, Checkout checkout) =>
        JsonSerializer.Serialize(new { id, type = "checkout.expired", checkoutId = checkout.CheckoutId });

    private async Task<HttpResponseMessage> DeliverAsync(string payload, string? signature = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payment-events");

        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add("Stripe-Signature", signature ?? Sign(payload));

        return await Client.SendAsync(request, Token);
    }

    private static string Sign(string payload) =>
        Convert.ToHexString(
                HMACSHA256.HashData(
                    Encoding.UTF8.GetBytes(ApiFactory.PaymentWebhookSecret), Encoding.UTF8.GetBytes(payload)))
            .ToLower(CultureInfo.InvariantCulture);

    private async Task<Checkout> CheckOutAsync(string accessToken, Guid roomId, int startingIn = 30)
    {
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(startingIn);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new
        {
            items = new[] { new { roomId, checkIn, checkOut = checkIn.AddDays(Nights), adults = 2, children = 0 } }
        });

        using var response = await Client.SendAsync(request, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var booking = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
        var payment = booking.GetProperty("payment");
        var bookingId = booking.GetProperty("id").GetGuid();

        return new Checkout(
            bookingId,
            await ScalarAsync<string>($"SELECT ProviderCheckoutId AS Value FROM Payments WHERE BookingId = '{bookingId}'"),
            payment.GetProperty("amount").GetDecimal(),
            payment.GetProperty("currency").GetString()!);
    }

    private Task<int> ScalarAsync(string sql) => ScalarAsync<int>(sql);

    private async Task<T> ScalarAsync<T>(string sql)
    {
        using var scope = Factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>();

        return await context.Database.SqlQueryRaw<T>(sql).SingleAsync(Token);
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
            Occupancy.Create(2, 0).Value,
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
