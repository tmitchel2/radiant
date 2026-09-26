using System.Numerics;
using Radiant.Gallery;
using Radiant.Theming;
using Radiant.UI.Driver;
using Radiant.UI.Driver.MSTest;

namespace Radiant.UI.Automation.Tests;

/// <summary>The gallery, driven end to end in-process as a test would drive any app.</summary>
[TestClass]
public sealed class GalleryTests : RadiantUITest
{
    [TestInitialize]
    public void Start()
    {
        var themes = new ThemeController();
        Use(AppDriver.InProcess(new ThemeProvider(themes, new GalleryApp(themes)), new InProcessOptions { Size = new Vector2(1200, 800), AppName = "gallery" }));
    }

    [TestMethod]
    public async Task PressingAButtonCountsIt()
    {
        await Driver.Get("role=button label=Filled").TapAsync();

        await Driver.ByText("Filled pressed 1 times").Expect().ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task TheSidebarNavigates()
    {
        await Driver.Get("role=tab label=\"Sign in\"").TapAsync();

        await Driver.Get("role=tab label=\"Sign in\" selected").Expect().ToExistAsync();
        await Driver.Get("role=tab label=Components selected").Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task ADestinationBelowTheWindowCannotBeReached()
    {
        // The sidebar doesn't scroll (docs/improvements.md), so at this height its last items are
        // off the window: the tap says so rather than pressing something else.
        var error = await Assert.ThrowsAsync<AppDriverException>(() => Driver.Get("role=tab label=Docking").TapAsync());

        Assert.AreEqual(Radiant.Host.AgentControlProtocol.AgentErrorCodes.NotVisible, error.Code);
        StringAssert.Contains(error.Message, "tab \"Docking\"");
    }

    [TestMethod]
    public async Task TheCommandPaletteRunsACommand()
    {
        await Driver.KeyAsync("Cmd+K");
        await Driver.Get("role=dialog").Expect().ToBeVisibleAsync();

        await Driver.TypeAsync("Mail");
        await Driver.KeyAsync("Enter");

        await Driver.Get("role=dialog").Expect().ToBeGoneAsync();
        await Driver.Get("role=tab label=Mail selected").Expect().ToExistAsync();
    }

    [TestMethod]
    public async Task ADialogOpensAndEscapeClosesIt()
    {
        await Driver.Get("role=button label=\"Open dialog\"").TapAsync();
        await Driver.Get("role=dialog").Expect().ToBeVisibleAsync();

        await Driver.KeyAsync("Escape");

        await Driver.Get("role=dialog").Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task ATooltipShowsAfterItsDelay()
    {
        await Driver.Get("role=button label=Notifications").HoverAsync();

        await Driver.Get("role=tooltip").Expect().ToBeVisibleAsync();
    }
}
