using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HotelBooking.Api.IntegrationTests.Infrastructure;
using HotelBooking.Application.Authentication.Dtos;

namespace HotelBooking.Api.IntegrationTests.Sessions;

public sealed class SessionEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private const string Email = "abdalkreem@example.com";

    [Fact]
    public async Task Post_TheIssuedAccessToken_ActuallyAuthenticatesAProtectedRouteAsThatUser()
    {
        var registration = await RegisterAsync(Email);
        var userId = (await registration.Content.ReadFromJsonAsync<Application.Users.Dtos.UserDto>(
            TestContext.Current.CancellationToken))!.Id;

        var session = await SignUpAndLogInAsync("karim@example.com");

        var probe = await GetProbeAsync(session.AccessToken);

        probe.StatusCode.ShouldBe(HttpStatusCode.OK);

        var whoami = await probe.Content.ReadFromJsonAsync<ProbeBody>(
            TestContext.Current.CancellationToken);

        whoami.ShouldNotBeNull();
        whoami.UserId.ShouldNotBe(userId);
        whoami.Role.ShouldBe("User");
        whoami.Jti.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Put_WhenAReplayIsDetected_AlsoKillsTheTokenTheLegitimateClientIsHolding()
    {
        var session = await SignUpAndLogInAsync(Email);

        var rotated = await ReadSessionAsync(await RefreshAsync(session.RefreshToken));

        (await RefreshAsync(session.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var afterDetection = await RefreshAsync(rotated.RefreshToken);

        afterDetection.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> GetProbeAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/api" + ProtectedProbeEndpoint.Route);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<SessionDto> ReadSessionAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<SessionDto>(
            TestContext.Current.CancellationToken))!;
    }

    private sealed record ProbeBody(Guid UserId, string? Role, string? Jti);
}
