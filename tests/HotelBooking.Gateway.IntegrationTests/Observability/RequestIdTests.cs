using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HotelBooking.Gateway.IntegrationTests.Observability;

public sealed class RequestIdTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_WhenTheGatewayAnswersItself_ReturnsOneTraceIdInHeaderAndBody()
    {
        using var client = factory
            .WithWebHostBuilder(host => host.UseEnvironment("Testing"))
            .CreateClient();

        using var response = await client.GetAsync("/api/hotels", Token);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable, "the cluster has no destination in this test");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
        var requestId = response.Headers.GetValues("X-Request-Id").Single();

        requestId.ShouldMatch("^[0-9a-f]{32}$", "the bare trace id, not the whole traceparent");
        problem.GetProperty("traceId").GetString().ShouldBe(requestId);
    }
}
