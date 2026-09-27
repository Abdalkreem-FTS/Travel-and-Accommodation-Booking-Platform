using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace HotelBooking.Gateway.IntegrationTests.Forwarding;

public sealed class RouteTableTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplication _upstream = CreateUpstream();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _upstream.Urls.Add("http://127.0.0.1:0");
        _upstream.Run(context => context.Response.WriteAsync("upstream", Token));

        await _upstream.StartAsync(Token);
    }

    public async ValueTask DisposeAsync() => await _upstream.DisposeAsync();

    private static WebApplication CreateUpstream()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();

        return builder.Build();
    }

    [Theory]
    [InlineData("GET", "/api/hotels", "api-1")]
    [InlineData("POST", "/api/sessions", "api-1")]
    [InlineData("GET", "/openapi/v1.json", "api-1")]
    [InlineData("GET", "/scalar", "api-1")]
    [InlineData("GET", "/health/ready", "api-1")]
    [InlineData("GET", "/", "web-1")]
    [InlineData("GET", "/bookings/123", "web-1")]
    [InlineData("GET", "/assets/index-abc123.js", "web-1")]
    public async Task Request_IsForwardedToTheClusterThatOwnsItsPath(string method, string path, string destination)
    {
        var address = _upstream.Urls.Single();

        using var client = factory
            .WithWebHostBuilder(host => host
                .UseEnvironment("Testing")
                .UseSetting("Gateway:ExposeUpstream", "true")
                .UseSetting("ReverseProxy:Clusters:api:Destinations:api-1:Address", address)
                .UseSetting("ReverseProxy:Clusters:web:Destinations:web-1:Address", address))
            .CreateClient();

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request, Token);

        response.Headers.GetValues("X-Upstream-Instance").Single().ShouldBe(destination);
    }
}
