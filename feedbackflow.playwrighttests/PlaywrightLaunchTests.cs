using System.Threading.Tasks;
using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FeedbackFlow.PlaywrightTests;

[TestClass]
public class PlaywrightLaunchTests : PageTest
{
    [TestMethod]
    public async Task App_Launches_MainPage()
    {
        // Navigate to the local app. Ensure the app is running at http://localhost:5265
        var response = await Page.GotoAsync("http://localhost:5265", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        Assert.IsNotNull(response);
        Assert.IsTrue(response.Ok);

        // Take a screenshot for verification
        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = "playwright-screenshot.png", FullPage = true });
    }
}
