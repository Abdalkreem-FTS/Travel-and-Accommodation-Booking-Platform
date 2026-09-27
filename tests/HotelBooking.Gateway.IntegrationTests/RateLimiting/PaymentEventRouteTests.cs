using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HotelBooking.Gateway.IntegrationTests.RateLimiting;

public sealed class PaymentEventRouteTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const int Deliveries = 3;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_PaymentEvents_IsNeverThrottledWhileEveryOtherRouteIs()
    {
        using var client = factory
            .WithWebHostBuilder(host => host
                .UseEnvironment("Testing")
                .UseSetting("RateLimits:Global:Permit", "1"))
            .CreateClient();

        for (var delivery = 0; delivery < Deliveries; delivery++)
        {
            using var content = new StringContent("{}");
            using var response = await client.PostAsync("/api/payment-events", content, Token);

            response.StatusCode.ShouldNotBe(
                HttpStatusCode.TooManyRequests,
                "every provider delivery comes from a handful of addresses, so a per-IP limit would drop them");
        }

        using var first = await client.GetAsync("/api/hotels", Token);
        using var second = await client.GetAsync("/api/hotels", Token);

        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests, "the global limit still guards everything else");
    }
}
