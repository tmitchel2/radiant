using System.Numerics;
using Radiant.Gallery;
using Radiant.Theming;
using Radiant.UI.Driver;
using Radiant.UI.Driver.MSTest;

namespace Radiant.UI.Automation.Tests.Gallery;

/// <summary>The gallery in a small window: everything can still be reached.</summary>
[TestClass]
public sealed class SmallWindowTests : RadiantUITest
{
    [TestInitialize]
    public void Start()
    {
        var themes = new ThemeController();
        Use(AppDriver.InProcess(new ThemeProvider(themes, new GalleryApp(themes)), new InProcessOptions { Size = new Vector2(900, 560), AppName = "gallery" }));
    }

    [TestMethod]
    [DataRow("Components")]
    [DataRow("Dashboard")]
    [DataRow("Settings")]
    [DataRow("Table")]
    [DataRow("Landing page")]
    [DataRow("Store")]
    [DataRow("Workspace")]
    [DataRow("Mail")]
    [DataRow("Preferences")]
    [DataRow("Docking")]
    [DataRow("Theme")]
    public async Task EveryPageFitsAndItsLastControlCanBeReached(string destination)
    {
        await Driver.NavigationDrawer().Item().WithLabel(new Radiant.Host.AgentControlProtocol.TextMatch("^" + destination, Radiant.Host.AgentControlProtocol.TextMatchMode.Regex)).TapAsync();
        await Driver.WaitForIdleAsync();

        var root = (await Driver.TreeAsync("basic,geometry", depth: 1)).Nodes.Single();
        Assert.IsTrue(root.Children!.All(c => c.Bounds!.X + c.Bounds.W <= 900.5f && c.Bounds.Y + c.Bounds.H <= 560.5f),
            "the shell fits: " + string.Join(", ", root.Children!.Select(c => $"{c.Role} {c.Bounds}")));
        var focusable = await Driver.Get("@SidebarLayout.Page >> enabled").QueryAsync();
        var last = await Driver.Get(new Radiant.Host.AgentControlProtocol.Selector { Id = focusable.Matches.Where(m => m.Role is not ("text" or "heading" or "none" or "group" or "image")).LastOrDefault()?.Id ?? focusable.Matches.Last().Id }).ScrollIntoViewAsync();
        Assert.IsNotNull(last.Target);
    }
}
