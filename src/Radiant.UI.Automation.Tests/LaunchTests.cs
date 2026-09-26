using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Driver;
using Radiant.UI.Driver.MSTest;

namespace Radiant.UI.Automation.Tests;

/// <summary>The gallery built, launched as its own process and driven: the whole way an e2e suite would go.</summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class LaunchTests : RadiantUITest
{
    private static string GalleryProject => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Radiant.Gallery"));

    [TestMethod]
    [DataRow(AgentTransport.Socket)]
    [DataRow(AgentTransport.File)]
    public async Task TheGalleryLaunchesHeadlessAndIsDriven(AgentTransport transport)
    {
        await LaunchAsync(new AgentLaunchOptions { Target = GalleryProject, Headless = true }, transport);

        await Driver.VerticalSlice().Filled().TapAsync();
        await Driver.Role.Text("Filled pressed 1 times").Expect().ToBeVisibleAsync();
        await Driver.NavigationDrawer().Item().WithLabel("Sign in").TapAsync();
        await Driver.SignInForm().Email().TypeAsync("ada@example.com");
        await Driver.SignInForm().Email().Input().Expect().ToHaveValueAsync("ada@example.com");
        Assert.IsTrue((await Driver.InfoAsync()).Headless);
    }
}
