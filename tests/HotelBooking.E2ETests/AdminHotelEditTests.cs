using Microsoft.Data.SqlClient;
using Microsoft.Playwright;

using static Microsoft.Playwright.Assertions;

namespace HotelBooking.E2ETests;

// Needs E2E_SQL_CONNECTION: no endpoint grants the first admin, so the test writes the role row
// itself, the same way the demo does.
[Trait("Category", "E2E")]
public sealed class AdminHotelEditTests(BrowserFixture browser)
{
    private const int AdminRole = 2;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnAdmin_EditsAHotelDescription_AndTheEditSurvivesAReload()
    {
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var email = BrowserFixture.UniqueEmail("khaled.haddad");
        await BrowserFixture.RegisterAsync(context, "Khaled", "Haddad", email);
        // Before logging in: the role rides in the token, so it has to exist when the token is issued.
        await GrantAdminAsync(email);
        await BrowserFixture.LogInAsync(page, email);

        // A city and hotel from the development seed data, so the test never depends on list order.
        await page.GotoAsync("/admin/cities");
        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Amman", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Qasr Al-Jabal", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 })).ToHaveTextAsync("Edit Qasr Al-Jabal");

        var description = $"Edited by the end-to-end test {Guid.NewGuid():N}.";
        await page.GetByLabel("Description").FillAsync(description);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Status).Filter(new LocatorFilterOptions { HasText = "Saved." })).ToBeVisibleAsync();

        // A reload reads the hotel back from the server, not from what the form still holds.
        await page.ReloadAsync();
        await Expect(page.GetByLabel("Description")).ToHaveValueAsync(description);
    }

    private static async Task GrantAdminAsync(string email)
    {
        var connectionString = Environment.GetEnvironmentVariable("E2E_SQL_CONNECTION")
            ?? throw new InvalidOperationException("Set E2E_SQL_CONNECTION to the compose SQL Server's connection string.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(Token);
        await using var command = new SqlCommand(
            "INSERT INTO UserRoles (UserId, Role, GrantedAtUtc) SELECT Id, @role, SYSDATETIMEOFFSET() FROM Users WHERE Email = @email",
            connection);
        command.Parameters.AddWithValue("@role", AdminRole);
        command.Parameters.AddWithValue("@email", email);

        (await command.ExecuteNonQueryAsync(Token)).ShouldBe(1, $"exactly one user should have the email {email}");
    }
}
