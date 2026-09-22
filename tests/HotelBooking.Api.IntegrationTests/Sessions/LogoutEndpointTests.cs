using System.Net;
using System.Net.Http.Headers;

using HotelBooking.Api.IntegrationTests.Infrastructure;

namespace HotelBooking.Api.IntegrationTests.Sessions;

public sealed class LogoutEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private const string Email = "abdalkreem@example.com";

    [Fact]
    public async Task Delete_ForALiveSession_Returns204AndTheAccessTokenStopsWorkingImmediately()
    {
        var session = await SignUpAndLogInAsync(Email);

        (await GetProbeAsync(session.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await LogoutAsync(session.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetProbeAsync(session.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_WhenTheDenylistIsUnreachable_IsRefusedThoughReadsAreStillServed()
    {
        var session = await SignUpAndLogInAsync(Email);

        using var degraded = Factory.CreateClientWithUnreachableCache();

        using var read = new HttpRequestMessage(HttpMethod.Get, "/api" + ProtectedProbeEndpoint.Route);
        read.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var readResponse = await degraded.SendAsync(read, TestContext.Current.CancellationToken);

        readResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var mutation = await LogoutAsync(session.AccessToken, degraded);

        mutation.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> GetProbeAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/api" + ProtectedProbeEndpoint.Route);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

}
