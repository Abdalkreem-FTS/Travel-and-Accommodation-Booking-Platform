using Microsoft.Playwright;

using static Microsoft.Playwright.Assertions;

[assembly: AssemblyFixture(typeof(HotelBooking.E2ETests.BrowserFixture))]

namespace HotelBooking.E2ETests;

// One Chromium for the whole run. Every test opens its own context, a fresh browser profile with
// no storage, so no test ever sees another test's login.
public sealed class BrowserFixture : IAsyncLifetime
{
    public const string Password = "correct-horse-battery-staple";

    // The running compose stack, entered through the gateway like a real guest.
    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:8080";

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async ValueTask InitializeAsync()
    {
        // Downloads Chromium on the first run; a no-op once it is there.
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Installing Chromium for Playwright failed with exit code {exitCode}.");
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = Environment.GetEnvironmentVariable("E2E_HEADED") != "1" });
    }

    public Task<IBrowserContext> NewContextAsync() =>
        _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = BaseUrl });

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }

    // A new address every run, so the tests never trip over accounts from an earlier run.
    public static string UniqueEmail(string name) => $"{name}.{Guid.NewGuid():N}@example.com";

    // Through the API, not the form: registering in the browser has a test of its own.
    public static async Task RegisterAsync(IBrowserContext context, string firstName, string lastName, string email)
    {
        var response = await context.APIRequest.PostAsync(
            "/api/users",
            new APIRequestContextOptions { DataObject = new { firstName, lastName, email, password = Password } });

        response.Ok.ShouldBeTrue($"registering {email} returned {response.Status}: {await response.TextAsync()}");
    }

    public static async Task LogInAsync(IPage page, string email)
    {
        await page.GotoAsync("/login");
        await page.GetByLabel("Email", new PageGetByLabelOptions { Exact = true }).FillAsync(email);
        await page.GetByLabel("Password", new PageGetByLabelOptions { Exact = true }).FillAsync(Password);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Log in", Exact = true }).ClickAsync();

        await Expect(page.GetByRole(AriaRole.Navigation, new PageGetByRoleOptions { Name = "Account" })).ToContainTextAsync(email);
    }
}
