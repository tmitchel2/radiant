using System.Numerics;
using System.Text.RegularExpressions;
using Radiant.Gallery;
using Radiant.Host.AgentControlProtocol;
using Radiant.Theming;
using Radiant.UI.Driver;
using Radiant.UI.Driver.MSTest;

namespace Radiant.UI.Automation.Tests.Gallery;

/// <summary>The gallery, run in-process at a desktop size, and the ways around it its tests share.</summary>
public abstract class GalleryTest : RadiantUITest
{
    /// <summary>The gallery's theme, to check what its switches and commands do to it.</summary>
    protected ThemeController Themes { get; private set; } = null!;

    [TestInitialize]
    public void StartGallery()
    {
        Themes = new ThemeController(ThemePresets.Tonal);
        Use(AppDriver.InProcess(new ThemeProvider(Themes, new GalleryApp(Themes)), new InProcessOptions { Size = new Vector2(1200, 800), AppName = "gallery" }));
    }

    /// <summary>The sidebar's destination named <paramref name="label"/> (a count after it, as the store has, is allowed).</summary>
    protected Locator Destination(string label) =>
        Driver.NavigationDrawer().Item().WithLabel(new TextMatch($"^{Regex.Escape(label)}( \\d+)?$", TextMatchMode.Regex));

    /// <summary>Goes to a destination from the sidebar, and waits until it's the chosen one and its page is titled.</summary>
    protected async Task GoToAsync(string destination, string? title = null)
    {
        await Destination(destination).TapAsync();
        await Destination(destination).Expect().ToBeSelectedAsync();
        await Title(title ?? destination).Expect().ToExistAsync();
    }

    /// <summary>The app bar showing <paramref name="title"/>.</summary>
    protected Locator Title(string title) => Driver.SidebarLayout().AppBar().Containing(title);

    /// <summary>What shows exactly <paramref name="text"/>: text, a heading, a labelled control.</summary>
    protected Locator Text(string text) => Driver.ByText(text);

    /// <summary>Scrolls <paramref name="locator"/> into view and waits until it can be seen, as someone reading down the page would.</summary>
    protected static async Task SeeAsync(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        await locator.Expect().ToExistAsync();
        await locator.ScrollIntoViewAsync();
        await locator.Expect().ToBeVisibleAsync();
    }
}
