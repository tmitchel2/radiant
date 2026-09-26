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
    public async Task ADestinationBelowTheWindowIsScrolledTo()
    {
        var before = await Driver.Get("role=tab label=Docking").InspectAsync("visibility");
        Assert.IsNull(before.Visible, "the sidebar starts scrolled to its top, Docking below the window");

        await Driver.Get("role=tab label=Docking").TapAsync();

        await Driver.Get("role=tab label=Docking selected").Expect().ToBeVisibleAsync();
        var root = (await Driver.TreeAsync("basic,geometry", depth: 1)).Nodes.Single();
        Assert.IsTrue(root.Children!.All(c => c.Bounds!.Y + c.Bounds.H <= 800.5f), "the shell fits the window");
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
