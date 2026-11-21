using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace E2E;

public class CheckoutTests : PageTest
{

    // Must be HTTP because of Docker
    // Quick compose setups skip HTTPS certificates inside the container, hence HTTP only here
    private const string BaseUrl = "http://localhost:5106";

    [Test]
    public async Task AddItemToBasket_Checkout_Pay_Succeeds()
    {
        await Page.GotoAsync(BaseUrl);

        await Page.GotoAsync($"{BaseUrl}/Identity/Account/Login");
        await Page.FillAsync("#Input_Email", "demouser@microsoft.com");
        await Page.FillAsync("#Input_Password", "Pass@word1");
        await Page.ClickAsync("button:has-text('Log in')");
        Console.WriteLine("URL after login: " + Page.Url);
        await Page.ScreenshotAsync(new() { Path = "after-login.png" });

        await Page.GotoAsync(BaseUrl);

        await Page.Locator("input[value='[ ADD TO BASKET ]']").First.ClickAsync(); // Find first item on page and add, based on _product.cshtml
        await Page.ClickAsync("a:has-text('Checkout')"); // From Pages/Basket/Checkout.cshtml
        Console.WriteLine("URL after checkout click: " + Page.Url);
        await Page.ScreenshotAsync(new() { Path = "after-checkout.png" });

        await Page.ClickAsync("input[value='[ Pay Now ]']"); // From Pages/Basket/Checkout.cshtml

        await Expect(Page.Locator("h1:has-text('Thanks for your Order!')")).ToBeVisibleAsync(); // From Success.cshtml
    }

}
