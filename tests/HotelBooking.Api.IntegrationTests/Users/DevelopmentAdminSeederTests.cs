using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using HotelBooking.Api.IntegrationTests.Infrastructure;
using HotelBooking.Application.Authentication.Dtos;
using HotelBooking.Infrastructure.Persistence;
using HotelBooking.Infrastructure.Persistence.Seeding;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HotelBooking.Api.IntegrationTests.Users;

public sealed class DevelopmentAdminSeederTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private const string AdminEmail = "abdalkreem@example.com";

    private const string AdminPassword = "abdalkreem-mahmoud-bzoor";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seed_TwiceOnStartup_LeavesOneAccountThatCanUseTheAdminRoutes()
    {
        await Factory.Services.SeedDevelopmentAdminAsync(AdminEmail, AdminPassword, Token);
        await Factory.Services.SeedDevelopmentAdminAsync(AdminEmail, AdminPassword, Token);

        using var login = await LoginAsync(AdminEmail, AdminPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var session = (await login.Content.ReadFromJsonAsync<SessionDto>(Token))!;

        using var response = await SendAsync(HttpMethod.Get, $"/api/users?email={AdminEmail}", session.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, "an admin-only route answers the seeded account");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        body.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString())
            .ShouldBe(["User", "Admin"], ignoreOrder: true);

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>();

        (await context.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM Users").SingleAsync(Token))
            .ShouldBe(1, "the second startup finds the account instead of failing or adding another");
    }
}
