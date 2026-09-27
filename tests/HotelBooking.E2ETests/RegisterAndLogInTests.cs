using Microsoft.Playwright;

using static Microsoft.Playwright.Assertions;

namespace HotelBooking.E2ETests;

[Trait("Category", "E2E")]
public sealed class RegisterAndLogInTests(BrowserFixture browser)
{
    [Fact]
    public async Task ANewGuest_RegistersLogsOutAndLogsBackIn()
    {
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var email = BrowserFixture.UniqueEmail("abdalkreem.bzoor");
        var account = page.GetByRole(AriaRole.Navigation, new PageGetByRoleOptions { Name = "Account" });

        await page.GotoAsync("/register");
        await page.GetByLabel("First name").FillAsync("Abdalkreem");
        await page.GetByLabel("Last name").FillAsync("Bzoor");
        await page.GetByLabel("Email", new PageGetByLabelOptions { Exact = true }).FillAsync(email);
        await page.GetByLabel("Password", new PageGetByLabelOptions { Exact = true }).FillAsync(BrowserFixture.Password);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create account" }).ClickAsync();

        // Registering logs the guest straight in.
        await Expect(account).ToContainTextAsync(email);

        await account.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Log out" }).ClickAsync();
        await Expect(account.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Log in" })).ToBeVisibleAsync();
        await Expect(account).Not.ToContainTextAsync(email);

        await BrowserFixture.LogInAsync(page, email);
    }
}
