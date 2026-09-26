using Radiant.Host.AgentControlProtocol;
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

        await Driver.Get("role=button label=Filled").TapAsync();
        await Driver.ByText("Filled pressed 1 times").Expect().ToBeVisibleAsync();
        await Driver.Get("role=tab label=\"Sign in\"").TapAsync();
        await Driver.Get("role=textField label=Email").TypeAsync("ada@example.com");
        await Driver.Get("role=textField label=Email").Expect().ToHaveValueAsync("ada@example.com");
        Assert.IsTrue((await Driver.InfoAsync()).Headless);
    }
}
