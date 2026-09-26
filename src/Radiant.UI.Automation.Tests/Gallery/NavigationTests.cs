using Radiant.UI.Driver;

namespace Radiant.UI.Automation.Tests.Gallery;

/// <summary>Getting around the gallery: the sidebar, shortcuts, the command palette and the theme.</summary>
[TestClass]
public sealed class NavigationTests : GalleryTest
{
    [TestMethod]
    [DataRow("Dashboard")]
    [DataRow("Settings")]
    [DataRow("Sign in")]
    [DataRow("Empty state")]
    [DataRow("Table")]
    [DataRow("Landing page")]
    [DataRow("Store")]
    [DataRow("Workspace")]
    [DataRow("Mail")]
    [DataRow("New project")]
    [DataRow("Preferences")]
    [DataRow("Docking")]
    [DataRow("Components")]
    public async Task EveryDestinationOpensItsPage(string destination)
    {
        if (destination == "Components")
        {
            await GoToAsync("Dashboard");
        }

        await GoToAsync(destination);

        Assert.AreEqual(1, await Driver.NavigationDrawer().Item().Selected().CountAsync(), "one destination chosen at a time");
    }

    [TestMethod]
    public async Task ADestinationBelowTheFoldIsScrolledToAndTheShellFitsTheWindow()
    {
        var before = await Destination("Docking").InspectAsync("visibility");
        Assert.IsNull(before.Visible, "the sidebar starts scrolled to its top");

        await GoToAsync("Docking");

        var root = (await Driver.TreeAsync("basic,geometry", depth: 1)).Nodes.Single();
        Assert.IsTrue(root.Children!.All(c => c.Bounds!.Y + c.Bounds.H <= 800.5f), "the shell fits the window");
    }

    [TestMethod]
    [DataRow("Cmd+1", "Components")]
    [DataRow("Cmd+2", "Dashboard")]
    [DataRow("Cmd+4", "Sign in")]
    [DataRow("Cmd+9", "Workspace")]
    public async Task ShortcutsGoToPages(string chord, string destination)
    {
        if (destination == "Components")
        {
            await GoToAsync("Dashboard");
        }

        await Driver.KeyAsync(chord);

        await Destination(destination).Expect().ToBeSelectedAsync();
        await Title(destination).Expect().ToExistAsync();
    }

    [TestMethod]
    public async Task TheSearchButtonOpensTheCommandPaletteAndEscapeClosesIt()
    {
        await Driver.GalleryApp().Search().TapAsync();
        await Driver.CommandPalette().Search().Expect().ToBeFocusedAsync();

        await Driver.KeyAsync("Escape");

        await Driver.CommandPalette().Search().Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task TheCommandPaletteFindsAndRunsACommand()
    {
        await Driver.KeyAsync("Cmd+K");
        await Driver.CommandPalette().Search().Expect().ToBeFocusedAsync();

        await Driver.TypeAsync("Mail");
        await Driver.CommandPalette().Result().Nth(0).Expect().ToContainTextAsync("Mail");
        await Driver.KeyAsync("Enter");

        await Driver.CommandPalette().Search().Expect().ToBeGoneAsync();
        await Destination("Mail").Expect().ToBeSelectedAsync();
    }

    [TestMethod]
    public async Task TheCommandPaletteSaysWhenNothingMatches()
    {
        await Driver.KeyAsync("Cmd+K");

        await Driver.TypeAsync("zzzz nothing");

        Assert.AreEqual(0, await Driver.CommandPalette().Result().CountAsync());
    }

    [TestMethod]
    public async Task TheDarkThemeShortcutTogglesTheTheme()
    {
        Assert.IsFalse(Themes.Theme.Colors.IsDark);

        await Driver.KeyAsync("Cmd+Shift+D");
        await Driver.WaitForIdleAsync();

        Assert.IsTrue(Themes.Theme.Colors.IsDark);
    }

    [TestMethod]
    public async Task ATooltipShowsAfterItsDelay()
    {
        await Driver.GalleryApp().Notifications().HoverAsync();

        await Driver.Role.Tooltip("Notifications").Expect().ToBeVisibleAsync();
    }
}
