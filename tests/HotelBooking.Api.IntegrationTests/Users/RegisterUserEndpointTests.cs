using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using HotelBooking.Api.IntegrationTests.Infrastructure;
using HotelBooking.Application.Users.Dtos;

namespace HotelBooking.Api.IntegrationTests.Users;

public sealed class RegisterUserEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Post_WithAValidBody_Returns201WithTheUserAndItsLocation()
    {
        var response = await RegisterAsync("abdalkreem@example.com");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var user = await response.Content.ReadFromJsonAsync<UserDto>(
            TestContext.Current.CancellationToken);

        user.ShouldNotBeNull();
        user.Email.ShouldBe("abdalkreem@example.com");
        user.Roles.ShouldBe(["User"]);
        response.Headers.Location!.ToString().ShouldBe($"/api/users/{user.Id}");
    }

    [Fact]
    public async Task Post_TwentyTimesInParallelWithOneEmail_CreatesExactlyOneUserAndNeverA500()
    {
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => RegisterAsync("race@example.com")));

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(19);
        responses.ShouldNotContain(response => (int)response.StatusCode >= 500);

        (await LoginAsync("race@example.com")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    internal sealed record ProblemBody(
        string? Type,
        string? Title,
        int? Status,
        string? Detail,
        string? TraceId,
        string? ErrorCode,
        Dictionary<string, JsonElement>? Errors);
}
