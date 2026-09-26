using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class TreeViewTests
{
    private static readonly Vector2 Viewport = new(400, 400);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box { Layout = new LayoutStyle { FlexGrow = 1 }, Children = [element] }));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root)
    {
        for (var i = 0; i < 5; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static SemanticsNode[] Items(UIRoot root) => [.. All(root.GetSemantics()).Where(n => n.Role == SemanticsRole.TreeItem)];

    private static string[] Labels(UIRoot root) => [.. Items(root).Select(n => n.Label!)];

    private static SemanticsNode Item(UIRoot root, string label) => Items(root).Single(n => n.Label == label);

    private static Vector2 Centre(SemanticsNode node) => new(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);

    private static void Click(UIRoot root, Vector2 at)
    {
        root.PointerDown(at);
        root.PointerUp(at);
        Settle(root);
    }

    private static void Press(UIRoot root, KeyCode key)
    {
        root.KeyDown(key);
        Settle(root);
    }

    private static TreeNode[] Files =>
    [
        new("src", "src")
        {
            Icon = "folder",
            ExpandedIcon = "folder_open",
            Children =
            [
                new("app", "App.cs") { Icon = "code" },
                new("ui", "UI") { Icon = "folder", Children = [new("button", "Button.cs"), new("text", "Text.cs")] },
            ],
        },
        new("readme", "README.md") { Icon = "description" },
    ];

    [TestMethod]
    public void OnlyExpandedItemsShowTheirChildren()
    {
        using var root = Mount(new TreeView(Files) { InitialExpanded = new HashSet<string> { "src" }, Label = "Files" });

        CollectionAssert.AreEqual(new[] { "src", "App.cs", "UI", "README.md" }, Labels(root));
        Assert.AreEqual(true, Item(root, "src").Semantics.Expanded);
        Assert.AreEqual(false, Item(root, "UI").Semantics.Expanded);
        Assert.IsNull(Item(root, "App.cs").Semantics.Expanded, "a leaf neither expands nor collapses");
        Assert.AreEqual("2", Item(root, "UI").Semantics.Value, "its level");
        Assert.IsTrue(Item(root, "UI").Bounds.X == Item(root, "src").Bounds.X, "rows span the tree; the indent is inside");
    }

    [TestMethod]
    public void TheChevronExpandsWithoutSelecting()
    {
        var selected = new List<string>();
        using var root = Mount(new TreeView(Files) { OnSelect = n => selected.Add(n.Id) });

        Click(root, Centre(All(root.GetSemantics()).Single(n => n.Label == "Expand src")));

        CollectionAssert.AreEqual(new[] { "src", "App.cs", "UI", "README.md" }, Labels(root));
        Assert.AreEqual(0, selected.Count);
    }

    [TestMethod]
    public void APressSelectsAndADoubleClickOpensOrActivates()
    {
        var activated = new List<string>();
        using var root = Mount(new TreeView(Files) { OnActivate = n => activated.Add(n.Id) });

        Click(root, Centre(Item(root, "src")));
        var selected = Item(root, "src").Semantics.Selected;
        var at = Centre(Item(root, "src"));
        root.PointerDown(at);
        root.PointerUp(at);
        Settle(root);
        var readme = Centre(Item(root, "README.md"));
        root.PointerDown(readme);
        root.PointerUp(readme);
        root.PointerDown(readme);
        root.PointerUp(readme);
        Settle(root);

        Assert.IsTrue(selected);
        CollectionAssert.AreEqual(new[] { "src", "App.cs", "UI", "README.md" }, Labels(root), "a double click opened the folder");
        CollectionAssert.AreEqual(new[] { "readme" }, activated);
    }

    [TestMethod]
    public void ArrowsMoveExpandCollapseAndClimb()
    {
        var activated = new List<string>();
        using var root = Mount(new TreeView(Files) { OnActivate = n => activated.Add(n.Id) });
        Click(root, Centre(Item(root, "src")));

        Press(root, KeyCode.Right);
        var opened = Labels(root).Length;
        Press(root, KeyCode.Right);
        Press(root, KeyCode.Down);
        Press(root, KeyCode.Right);
        Press(root, KeyCode.Right);
        var deepest = Items(root).Single(n => n.Semantics.Selected).Label;
        Press(root, KeyCode.Enter);
        Press(root, KeyCode.Left);
        var parent = Items(root).Single(n => n.Semantics.Selected).Label;
        Press(root, KeyCode.Left);
        var closed = Labels(root);
        Press(root, KeyCode.End);

        Assert.AreEqual(4, opened, "Right opened src");
        Assert.AreEqual("Button.cs", deepest, "Right went into src, Down to UI, Right opened it and went in");
        CollectionAssert.AreEqual(new[] { "button" }, activated);
        Assert.AreEqual("UI", parent, "Left from a leaf climbs to its parent");
        CollectionAssert.AreEqual(new[] { "src", "App.cs", "UI", "README.md" }, closed, "then Left closes it");
        Assert.AreEqual("README.md", Items(root).Single(n => n.Semantics.Selected).Label);
    }

    [TestMethod]
    public void OwnedExpansionIsReportedNotKept()
    {
        var reported = new List<IReadOnlySet<string>>();
        using var root = Mount(new TreeView(Files) { Expanded = new HashSet<string>(), OnExpandedChange = reported.Add });

        Click(root, Centre(All(root.GetSemantics()).Single(n => n.Label == "Expand src")));

        CollectionAssert.AreEquivalent(new[] { "src" }, reported.Single().ToArray());
        CollectionAssert.AreEqual(new[] { "src", "README.md" }, Labels(root));
    }

    [TestMethod]
    public void ALargeOpenTreeBuildsOnlyWhatShows()
    {
        var folders = Enumerable.Range(0, 200).Select(f => new TreeNode($"f{f}", $"Folder {f}")
        {
            Children = [.. Enumerable.Range(0, 100).Select(i => new TreeNode($"f{f}/{i}", $"File {i}"))],
        }).ToArray();
        using var root = Mount(new TreeView(folders) { InitialExpanded = new HashSet<string>(folders.Select(f => f.Id)) });

        Assert.IsTrue(Items(root).Length < 30, $"{Items(root).Length} of 20,200 rows built");
    }
}
