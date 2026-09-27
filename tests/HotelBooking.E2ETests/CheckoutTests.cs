using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.Playwright;

using static Microsoft.Playwright.Assertions;

namespace HotelBooking.E2ETests;

// Needs the stack started with PAYMENT_PROVIDER=Stripe and Stripe test keys.
[Trait("Category", "E2E")]
public sealed partial class CheckoutTests(BrowserFixture browser)
{
    [Fact]
    public async Task AGuest_SearchesBooksAndLandsOnStripeCheckout()
    {
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var email = BrowserFixture.UniqueEmail("omar.khalil");
        await BrowserFixture.RegisterAsync(context, "Omar", "Khalil", email);
        await BrowserFixture.LogInAsync(page, email);

        // A random night months ahead: a checkout holds its rooms for 30 minutes, so fixed dates
        // would find the room taken on the next run.
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(Random.Shared.Next(60, 365));

        await page.GotoAsync("/hotels");
        await page.GetByLabel("Check-in").FillAsync(checkIn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        await page.GetByLabel("Check-out").FillAsync(checkIn.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Search" }).ClickAsync();

        // The results section is named by its heading, "5 hotels"; its first link is the first card.
        await page.GetByRole(AriaRole.Region, new PageGetByRoleOptions { NameRegex = HotelCount() })
            .GetByRole(AriaRole.Link).First.ClickAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add to cart" }).First.ClickAsync();
        await Expect(page.GetByRole(AriaRole.Status).Filter(new LocatorFilterOptions { HasText = "Added." })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Navigation, new PageGetByRoleOptions { Name = "Main" })
            .GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Cart" }).ClickAsync();
        var appOrigin = new Uri(page.Url).Authority;
        // The request that leaves the app, caught before it goes anywhere: the Fake provider's
        // address never resolves, so waiting for its page would only time out.
        var leaving = await page.RunAndWaitForRequestAsync(
            () => page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Check out" }).First.ClickAsync(),
            request => request.IsNavigationRequest && new Uri(request.Url).Authority != appOrigin);

        leaving.Url.ShouldStartWith(
            "https://checkout.stripe.com/",
            customMessage: "the stack must run with PAYMENT_PROVIDER=Stripe; the Fake provider has no page to land on");

        // Commit: Stripe answered with its page. Waiting for all of Stripe's scripts would only test Stripe.
        await page.WaitForURLAsync(StripeCheckout(), new PageWaitForURLOptions { WaitUntil = WaitUntilState.Commit });
    }

    [GeneratedRegex(@"^\d+ hotels?$")]
    private static partial Regex HotelCount();

    [GeneratedRegex(@"^https://checkout\.stripe\.com/")]
    private static partial Regex StripeCheckout();
}
