using Radiant.UI.Automation;

namespace Radiant.UI.Automation.Tests.Gallery;

/// <summary>Every page, checked for what a person with assistive technology, or a small window, would hit.</summary>
[TestClass]
public sealed class AuditTests : GalleryTest
{
    public static IEnumerable<object[]> Destinations =>
        new[] { "Components", "Dashboard", "Settings", "Sign in", "Empty state", "Table", "Landing page", "Store", "Workspace", "Mail", "New project", "Preferences", "Docking", "Studio", "Theme" }
            .Select(d => new object[] { d });

    [TestMethod]
    [DynamicData(nameof(Destinations))]
    public async Task EveryControlHasAName(string destination)
    {
        await OpenAsync(destination);

        var tree = await Driver.TreeAsync("basic,state");
        var nameless = Flatten(tree.Nodes.Single())
            .Where(n => n.Focusable == true && string.IsNullOrWhiteSpace(n.Label))
            .Select(n => $"{n.Role} #{n.Id}{(n.TestId is null ? "" : " @" + n.TestId)}")
            .ToList();

        Assert.AreEqual(0, nameless.Count, $"focusable, but with no name to hear: {string.Join(", ", nameless)}");
    }

    [TestMethod]
    [DynamicData(nameof(Destinations))]
    public async Task EveryControlHasARole(string destination)
    {
        await OpenAsync(destination);

        var tree = await Driver.TreeAsync("basic,state");
        var roleless = Flatten(tree.Nodes.Single())
            // A group says what it is by its name; anything else by its role.
            .Where(n => n.Focusable == true && (n.Role is null or "none" || n.Role == "group" && string.IsNullOrWhiteSpace(n.Label)))
            .Select(n => $"\"{n.Label}\" #{n.Id}{(n.TestId is null ? "" : " @" + n.TestId)}")
            .ToList();

        Assert.AreEqual(0, roleless.Count, $"focusable, but not saying what it is: {string.Join(", ", roleless)}");
    }

    [TestMethod]
    [DataRow("Quartz", "Components")]
    [DataRow("Quartz", "Studio")]
    [DataRow("Linen", "Components")]
    [DataRow("Linen", "Studio")]
    [DataRow("Linen", "Settings")]
    [DataRow("Quartz", "Theme")]
    [DataRow("Linen", "Theme")]
    public async Task EveryControlHasANameAndARoleInEveryTheme(string preset, string destination)
    {
        // Themes build some components differently (labels above fields, segmented tabs): they must still say what they are.
        Themes.Set(Themes.Theme.WithStyle(Radiant.Theming.ThemePresets.Find(preset)!));
        await OpenAsync(destination);

        var tree = await Driver.TreeAsync("basic,state");
        var unclear = Flatten(tree.Nodes.Single())
            .Where(n => n.Focusable == true && (string.IsNullOrWhiteSpace(n.Label) || n.Role is null or "none"))
            .Select(n => $"{n.Role} \"{n.Label}\" #{n.Id}{(n.TestId is null ? "" : " @" + n.TestId)}")
            .ToList();

        Assert.AreEqual(0, unclear.Count, $"focusable, but unnamed or without a role: {string.Join(", ", unclear)}");
    }

    [TestMethod]
    [DynamicData(nameof(Destinations))]
    public async Task ThePageFitsTheWindowsWidth(string destination)
    {
        await OpenAsync(destination);

        var root = (await Driver.TreeAsync("basic,geometry", depth: 1)).Nodes.Single();

        Assert.IsTrue(root.Children!.All(c => c.Bounds!.X + c.Bounds.W <= 1200.5f && c.Bounds.Y + c.Bounds.H <= 800.5f),
            string.Join(", ", root.Children!.Select(c => $"{c.Role} {c.Bounds}")));
    }

    private async Task OpenAsync(string destination)
    {
        if (destination != "Components")
        {
            await GoToAsync(destination);
        }
        await Driver.WaitForIdleAsync();
    }

    private static IEnumerable<InspectNode> Flatten(InspectNode node) => (node.Children ?? []).SelectMany(Flatten).Prepend(node);
}
