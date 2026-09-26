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
        await Driver.VerticalSlice().Filled().TapAsync();

        await Driver.Role.Text("Filled pressed 1 times").Expect().ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task TheSidebarNavigates()
    {
        await Driver.NavigationDrawer().Item().WithLabel("Sign in").TapAsync();

        await Driver.NavigationDrawer().Item().WithLabel("Sign in").Expect().ToBeSelectedAsync();
        await Driver.NavigationDrawer().Item().WithLabel("Components").Expect().ToBeUnselectedAsync();
    }

    [TestMethod]
    public async Task ADestinationBelowTheWindowIsScrolledTo()
    {
        var before = await Driver.NavigationDrawer().Item().WithLabel("Docking").InspectAsync("visibility");
        Assert.IsNull(before.Visible, "the sidebar starts scrolled to its top, Docking below the window");

        await Driver.NavigationDrawer().Item().WithLabel("Docking").TapAsync();

        await Driver.NavigationDrawer().Item().WithLabel("Docking").Selected().Expect().ToBeVisibleAsync();
        var root = (await Driver.TreeAsync("basic,geometry", depth: 1)).Nodes.Single();
        Assert.IsTrue(root.Children!.All(c => c.Bounds!.Y + c.Bounds.H <= 800.5f), "the shell fits the window");
    }

    [TestMethod]
    public async Task TheCommandPaletteRunsACommand()
    {
        await Driver.KeyAsync("Cmd+K");
        await Driver.CommandPalette().Search().Expect().ToBeFocusedAsync();

        await Driver.TypeAsync("Mail");
        await Driver.KeyAsync("Enter");

        await Driver.CommandPalette().Search().Expect().ToBeGoneAsync();
        await Driver.NavigationDrawer().Item().WithLabel("Mail").Expect().ToBeSelectedAsync();
    }

    [TestMethod]
    public async Task ADialogOpensAndEscapeClosesIt()
    {
        await Driver.VerticalSlice().OpenDialog().TapAsync();
        await Driver.Dialog().Panel().Expect().ToBeVisibleAsync();

        await Driver.KeyAsync("Escape");

        await Driver.Dialog().Panel().Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task ATooltipShowsAfterItsDelay()
    {
        await Driver.GalleryApp().Notifications().HoverAsync();

        await Driver.Role.Tooltip().Expect().ToBeVisibleAsync();
    }
}
