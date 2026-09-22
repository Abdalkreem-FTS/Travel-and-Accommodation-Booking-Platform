using System.Net;
using System.Net.Http.Json;

using HotelBooking.Api.IntegrationTests.Infrastructure;
using HotelBooking.Application.Authentication.Dtos;

namespace HotelBooking.Api.IntegrationTests.Sessions;

public sealed class RefreshTokenConcurrencyTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private const string Email = "abdalkreem@example.com";

    [Fact]
    public async Task Put_TwiceAtOnceWithTheSameToken_LetsExactlyOneThroughAndKillsTheFamily()
    {
        var session = await SignUpAndLogInAsync(Email);

        var responses = await Task.WhenAll(
            RefreshAsync(session.RefreshToken),
            RefreshAsync(session.RefreshToken));

        var statuses = responses.Select(response => response.StatusCode).ToArray();

        statuses.Count(status => status == HttpStatusCode.OK).ShouldBe(1);
        statuses.Count(status => status == HttpStatusCode.Unauthorized).ShouldBe(1);

        var winner = await ReadSessionAsync(
            responses.Single(response => response.StatusCode == HttpStatusCode.OK));

        (await RefreshAsync(winner.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<SessionDto> ReadSessionAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<SessionDto>(
            TestContext.Current.CancellationToken))!;
}
