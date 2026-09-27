using System.Net;
using System.Text.Json;

using HotelBooking.Api.IntegrationTests.Infrastructure;

namespace HotelBooking.Api.IntegrationTests.Users;

public sealed class FindUserEndpointTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private const string AdminEmail = "abdalkreem@example.com";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_ByEmail_FindsTheUserIgnoringCaseAndAnUnknownAddressIs404()
    {
        var admin = await SignUpAndLogInAsAdminAsync(AdminEmail);
        await SignUpAndLogInAsync("omar@example.com");

        using var found = await SendAsync(HttpMethod.Get, "/api/users?email=%20OMAR@Example.com%20", admin.AccessToken);
        using var unknown = await SendAsync(HttpMethod.Get, "/api/users?email=khaled@example.com", admin.AccessToken);

        found.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var foundBody = JsonDocument.Parse(await found.Content.ReadAsStringAsync(Token));
        var user = foundBody.RootElement;

        user.GetProperty("id").GetGuid().ShouldBe(await Factory.UserIdAsync("omar@example.com"));
        user.GetProperty("email").GetString().ShouldBe("omar@example.com");
        user.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ShouldBe(["User"]);

        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var unknownBody = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync(Token));
        unknownBody.RootElement.GetProperty("errorCode").GetString().ShouldBe("User.NotFound");
    }

    [Theory]
    [InlineData("/api/users")]
    [InlineData("/api/users?email=not-an-email")]
    public async Task Get_WithoutAUsableEmail_IsAValidationProblemOnTheEmailField(string route)
    {
        var admin = await SignUpAndLogInAsAdminAsync(AdminEmail);

        using var response = await SendAsync(HttpMethod.Get, route, admin.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        body.RootElement.GetProperty("errors").TryGetProperty("email", out _).ShouldBeTrue();
    }
}
