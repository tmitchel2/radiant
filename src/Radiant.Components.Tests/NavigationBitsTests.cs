using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class NavigationBitsTests
{
    private static readonly Vector2 Viewport = new(900, 400);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(20), AlignItems = Align.FlexStart },
            Children = [element],
        }));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root)
    {
        for (var i = 0; i < 10; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static IEnumerable<SemanticsNode> All(UIRoot root) => All(root.GetSemantics());

    private static SemanticsNode Find(UIRoot root, SemanticsRole role, string label) => All(root).Single(n => n.Role == role && n.Label == label);

    private static void Click(UIRoot root, SemanticsNode node)
    {
        var at = new Vector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
        root.PointerDown(at);
        root.PointerUp(at);
        Settle(root);
    }

    private static void Press(UIRoot root, KeyCode key)
    {
        root.KeyDown(key);
        Settle(root);
    }

    [TestMethod]
    public void ASearchFieldClearsFromItsButtonOrEscapeAndSubmitsOnEnter()
    {
        var submitted = new List<string>();
        using var root = Mount(new SearchField("Search mail") { OnSubmit = submitted.Add });
        Click(root, Find(root, SemanticsRole.TextField, "Search mail"));

        root.TextInput("invoice");
        Settle(root);
        Press(root, KeyCode.Enter);
        Click(root, Find(root, SemanticsRole.Button, "Clear search"));
        var cleared = Find(root, SemanticsRole.TextField, "Search mail").Semantics.Value;
        root.TextInput("x");
        Settle(root);
        Press(root, KeyCode.Escape);

        CollectionAssert.AreEqual(new[] { "invoice" }, submitted);
        Assert.AreEqual("", cleared);
        Assert.AreEqual("", Find(root, SemanticsRole.TextField, "Search mail").Semantics.Value, "Escape clears");
        Assert.IsTrue(Find(root, SemanticsRole.TextField, "Search mail").IsFocused, "focus stays");
        Assert.IsTrue(Find(root, SemanticsRole.TextField, "Search mail").Bounds.Height <= 36);
    }

    [TestMethod]
    public void ALinkIsALinkThatPresses()
    {
        var pressed = 0;
        using var root = Mount(new Link("Privacy policy", () => pressed++));

        Click(root, Find(root, SemanticsRole.Link, "Privacy policy"));
        Press(root, KeyCode.Enter);

        Assert.AreEqual(2, pressed, "a click, then Enter while focused");
    }

    [TestMethod]
    public void KeycapsSplitAChordAsThePlatformWritesIt()
    {
        var caps = Kbd.For(KeyChord.Command(KeyCode.P, KeyModifiers.Shift)).Keys;

        CollectionAssert.AreEqual(OperatingSystem.IsMacOS() ? new[] { "⇧", "⌘", "P" } : new[] { "Ctrl", "Shift", "P" }, caps);
    }

    [TestMethod]
    public void ABreadcrumbLinksToEachLevelAboveTheCurrentPage()
    {
        var went = new List<string>();
        using var root = Mount(new Breadcrumb([new Crumb("Home", () => went.Add("home")), new Crumb("Projects", () => went.Add("projects")), new Crumb("Radiant")]));

        Click(root, Find(root, SemanticsRole.Link, "Projects"));

        CollectionAssert.AreEqual(new[] { "projects" }, went);
        Assert.IsFalse(All(root).Any(n => n.Role == SemanticsRole.Link && n.Label == "Radiant"), "the current page isn't a link");
    }

    [TestMethod]
    public void ALongBreadcrumbFoldsItsMiddle()
    {
        Crumb[] crumbs = [.. Enumerable.Range(1, 7).Select(i => new Crumb($"Level {i}", i < 7 ? () => { } : null))];
        using var root = Mount(new Breadcrumb(crumbs) { MaxVisible = 4 });

        var folded = All(root).Where(n => n.Label?.StartsWith("Level", StringComparison.Ordinal) == true && n.Role is SemanticsRole.Link or SemanticsRole.Text).Select(n => n.Label).ToArray();
        Click(root, Find(root, SemanticsRole.Button, "Show all steps"));
        var all = All(root).Count(n => n.Label?.StartsWith("Level", StringComparison.Ordinal) == true && n.Role is SemanticsRole.Link or SemanticsRole.Text);

        CollectionAssert.AreEqual(new[] { "Level 1", "Level 6", "Level 7" }, folded);
        Assert.AreEqual(7, all);
    }

    [TestMethod]
    public void PagesShowTheEndsTheCurrentNeighbourhoodAndGaps()
    {
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, Pagination.Pages(5, 3, 1).ToArray(), "nothing to fold");
        CollectionAssert.AreEqual(new[] { 1, 0, 9, 10, 11, 0, 20 }, Pagination.Pages(20, 10, 1).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 0, 20 }, Pagination.Pages(20, 3, 1).ToArray(), "a gap of one page shows the page");
        CollectionAssert.AreEqual(new[] { 1 }, Pagination.Pages(1, 1, 1).ToArray());
    }

    [TestMethod]
    public void PaginationMovesAndStopsAtTheEnds()
    {
        var went = new List<int>();
        using var root = Mount(new Pagination(10, 1, went.Add));

        Click(root, Find(root, SemanticsRole.Button, "Previous page"));
        Click(root, Find(root, SemanticsRole.Button, "Next page"));
        Click(root, Find(root, SemanticsRole.Button, "Page 10"));

        CollectionAssert.AreEqual(new[] { 2, 10 }, went, "there's no page before the first");
        Assert.IsTrue(Find(root, SemanticsRole.Button, "Page 1, current").Semantics.Selected);
    }
}
