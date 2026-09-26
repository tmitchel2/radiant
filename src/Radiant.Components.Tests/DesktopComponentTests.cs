using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Platform;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class DesktopComponentTests
{
    private static readonly Vector2 Viewport = new(800, 400);

    private static UIRoot Mount(Element element, Vector2? viewport = null)
    {
        var size = viewport ?? Viewport;
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box { Layout = new LayoutStyle { FlexGrow = 1 }, Children = [element] }));
        Settle(root, size);
        return root;
    }

    private static void Settle(UIRoot root, Vector2? viewport = null)
    {
        var size = viewport ?? Viewport;
        root.Update(size);
        for (var i = 0; i < 20; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(size);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static IEnumerable<SemanticsNode> All(UIRoot root) => All(root.GetSemantics());

    private static SemanticsNode Handle(UIRoot root) => All(root).Single(n => n.Role == SemanticsRole.Separator);

    private static Vector2 Centre(SemanticsNode node) => new(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);

    private static void Drag(UIRoot root, Vector2 from, Vector2 to)
    {
        root.PointerMove(from);
        root.PointerDown(from);
        root.PointerMove((from + to) / 2);
        root.PointerMove(to);
        root.PointerUp(to);
        Settle(root);
    }

    private static void Click(UIRoot root, SemanticsNode node, PointerButton button = PointerButton.Left)
    {
        var at = Centre(node);
        root.PointerMove(at);
        root.PointerDown(at, button);
        root.PointerUp(at, button);
        Settle(root);
    }

    private static Box Pane(ElementRef reference) => new() { Ref = reference, Layout = new LayoutStyle { FlexGrow = 1 } };

    private static (Splitter Splitter, ElementRef First, ElementRef Second) Panes(Func<Splitter, Splitter>? configure = null)
    {
        var first = new ElementRef();
        var second = new ElementRef();
        var splitter = new Splitter(Pane(first), Pane(second)) { Label = "Sidebar" };
        return (configure is null ? splitter : configure(splitter), first, second);
    }

    [TestMethod]
    public void ASplitterStartsWithTheSizedPaneAtItsInitialSize()
    {
        var (splitter, first, second) = Panes();
        using var root = Mount(splitter);

        Assert.AreEqual(280f, first.Bounds.Width);
        Assert.AreEqual(800f - 280f - 1f, second.Bounds.Width, "the other pane takes the rest, after the line");
        Assert.AreEqual("280", Handle(root).Semantics.Value);
        Assert.AreEqual("Sidebar", Handle(root).Label);
    }

    [TestMethod]
    public void DraggingTheDividerResizesThePanes()
    {
        var (splitter, first, _) = Panes();
        using var root = Mount(splitter);

        Drag(root, Centre(Handle(root)), Centre(Handle(root)) + new Vector2(100, 40));

        Assert.AreEqual(380f, first.Bounds.Width, 1f);
    }

    [TestMethod]
    public void TheDividersGripReachesPastTheLineOnBothSides()
    {
        var (splitter, first, _) = Panes();
        using var root = Mount(splitter);

        Drag(root, new Vector2(277, 100), new Vector2(297, 100));
        var fromLeft = first.Bounds.Width;
        Drag(root, new Vector2(302, 100), new Vector2(282, 100));

        Assert.AreEqual(300f, fromLeft, 1f, "grabbed just left of the line, over the first pane");
        Assert.AreEqual(280f, first.Bounds.Width, 1f, "grabbed just right of it, over the second");
    }

    [TestMethod]
    public void DraggingStopsAtTheLimits()
    {
        var (splitter, first, second) = Panes(s => s with { MinSize = 150, MinOtherSize = 200 });
        using var root = Mount(splitter);

        Drag(root, Centre(Handle(root)), new Vector2(790, 100));
        var widest = first.Bounds.Width;
        Drag(root, Centre(Handle(root)), new Vector2(5, 100));

        Assert.AreEqual(800f - 1f - 200f, widest, 1f, "the other pane keeps its minimum");
        Assert.AreEqual(150f, first.Bounds.Width, 1f);
        Assert.IsTrue(second.Bounds.Width > 600f);
    }

    [TestMethod]
    public void TheSecondPaneCanBeTheSizedOne()
    {
        var (splitter, first, second) = Panes(s => s with { SizedPane = SplitterPane.Second, InitialSize = 200 });
        using var root = Mount(splitter);
        var before = second.Bounds.Width;

        Drag(root, Centre(Handle(root)), Centre(Handle(root)) - new Vector2(50, 0));

        Assert.AreEqual(200f, before);
        Assert.AreEqual(250f, second.Bounds.Width, 1f, "moving the divider left grows the right pane");
        Assert.AreEqual(800f - 1f - 250f, first.Bounds.Width, 1f);
    }

    [TestMethod]
    public void AStackedSplitterSizesHeights()
    {
        var (splitter, first, _) = Panes(s => s with { Orientation = Orientation.Vertical, InitialSize = 150 });
        using var root = Mount(splitter);

        Drag(root, Centre(Handle(root)), Centre(Handle(root)) + new Vector2(30, 60));

        Assert.AreEqual(210f, first.Bounds.Height, 1f);
        Assert.AreEqual(800f, first.Bounds.Width);
    }

    [TestMethod]
    public void ASqueezedSplitterShrinksTheSizedPaneFirst()
    {
        var (splitter, first, second) = Panes();
        using var root = Mount(splitter, new Vector2(320, 300));

        Assert.AreEqual(120f, second.Bounds.Width, 1f, "the other pane keeps its minimum");
        Assert.AreEqual(320f - 1f - 120f, first.Bounds.Width, 1f);
    }

    [TestMethod]
    public void TheKeyboardMovesTheDivider()
    {
        var (splitter, first, _) = Panes();
        using var root = Mount(splitter);
        Click(root, Handle(root));

        root.KeyDown(KeyCode.Right);
        Settle(root);
        var stepped = first.Bounds.Width;
        root.KeyDown(KeyCode.Left, KeyModifiers.Shift);
        Settle(root);
        var back = first.Bounds.Width;
        root.KeyDown(KeyCode.End);
        Settle(root);
        var end = first.Bounds.Width;
        root.KeyDown(KeyCode.Home);
        Settle(root);

        Assert.AreEqual(296f, stepped, 1f);
        Assert.AreEqual(232f, back, 1f, "Shift takes a larger step");
        Assert.AreEqual(800f - 1f - 120f, end, 1f);
        Assert.AreEqual(120f, first.Bounds.Width, 1f);
    }

    [TestMethod]
    public void DoubleClickingTheDividerRestoresTheInitialSize()
    {
        var (splitter, first, _) = Panes();
        using var root = Mount(splitter);
        Drag(root, Centre(Handle(root)), Centre(Handle(root)) + new Vector2(100, 0));

        var at = Centre(Handle(root));
        root.PointerDown(at);
        root.PointerUp(at);
        root.PointerDown(at);
        root.PointerUp(at);
        Settle(root);

        Assert.AreEqual(280f, first.Bounds.Width, 1f);
    }

    [TestMethod]
    public void AnOwnedSizeIsReportedNotKept()
    {
        var sizes = new List<float>();
        var (splitter, first, _) = Panes(s => s with { Size = 300, OnSizeChange = sizes.Add });
        using var root = Mount(splitter);

        Drag(root, Centre(Handle(root)), Centre(Handle(root)) + new Vector2(40, 0));

        Assert.AreEqual(340f, sizes.Last(), 1f);
        Assert.AreEqual(300f, first.Bounds.Width, "the owner didn't take the new size");
    }

    [TestMethod]
    public void TheDividerShowsAResizeCursor()
    {
        var (splitter, _, _) = Panes();
        using var root = Mount(splitter);

        root.PointerMove(Centre(Handle(root)));

        Assert.AreEqual(CursorShape.ResizeLeftRight, root.Cursor);
    }

    private static DocumentTab[] Documents =>
    [
        new("Program.cs") { Icon = "code" },
        new("README.md") { Icon = "description", Modified = true },
        new("notes.txt"),
    ];

    private static SemanticsNode Tab(UIRoot root, string label) => All(root).Single(n => n.Role == SemanticsRole.Tab && n.Label == label);

    private static SemanticsNode? CloseButton(UIRoot root, string label) =>
        All(root).SingleOrDefault(n => n.Role == SemanticsRole.Button && n.Label == $"Close {label}");

    [TestMethod]
    public void ChoosingADocumentTabSelectsIt()
    {
        var chosen = new List<int>();
        using var root = Mount(new DocumentTabs(Documents, 0, chosen.Add) { OnClose = _ => { } });

        Click(root, Tab(root, "notes.txt"));

        CollectionAssert.AreEqual(new[] { 2 }, chosen);
        Assert.IsTrue(Tab(root, "Program.cs").Semantics.Selected);
        Assert.IsFalse(Tab(root, "notes.txt").Semantics.Selected);
    }

    [TestMethod]
    public void TheChosenTabCanBeClosedWithoutChoosingIt()
    {
        var closed = new List<int>();
        var chosen = new List<int>();
        using var root = Mount(new DocumentTabs(Documents, 0, chosen.Add) { OnClose = closed.Add });

        Click(root, CloseButton(root, "Program.cs")!);

        CollectionAssert.AreEqual(new[] { 0 }, closed);
        Assert.AreEqual(0, chosen.Count);
    }

    [TestMethod]
    public void OtherTabsShowTheirCloseButtonWhenHovered()
    {
        using var root = Mount(new DocumentTabs(Documents, 0, null) { OnClose = _ => { } });
        var before = CloseButton(root, "notes.txt");

        root.PointerMove(Centre(Tab(root, "notes.txt")));
        Settle(root);

        Assert.IsNull(before);
        Assert.IsNotNull(CloseButton(root, "notes.txt"));
    }

    [TestMethod]
    public void AModifiedTabShowsADotUntilHovered()
    {
        using var root = Mount(new DocumentTabs(Documents, 0, null) { OnClose = _ => { } });
        var readme = Tab(root, "README.md").Bounds;
        var before = CloseButton(root, "README.md");

        root.PointerMove(Centre(Tab(root, "README.md")));
        Settle(root);

        Assert.IsNull(before);
        Assert.IsNotNull(CloseButton(root, "README.md"));
        Assert.AreEqual(readme, Tab(root, "README.md").Bounds, "the tab keeps its size as the dot becomes a button");
    }

    [TestMethod]
    public void AMiddleClickClosesATab()
    {
        var closed = new List<int>();
        using var root = Mount(new DocumentTabs(Documents, 0, null) { OnClose = closed.Add });

        Click(root, Tab(root, "README.md"), PointerButton.Middle);

        CollectionAssert.AreEqual(new[] { 1 }, closed);
    }

    [TestMethod]
    public void TabsThatCantCloseHaveNoCloseButton()
    {
        using var root = Mount(new DocumentTabs([new DocumentTab("Welcome") { Closable = false }], 0, null) { OnClose = _ => { } });

        Assert.IsNull(CloseButton(root, "Welcome"));
    }

    [TestMethod]
    public void ArrowsChooseTheNeighbouringTab()
    {
        var chosen = new List<int>();
        using var root = Mount(new DocumentTabs(Documents, 1, chosen.Add));
        Click(root, Tab(root, "README.md"));

        root.KeyDown(KeyCode.Right);
        root.KeyDown(KeyCode.Left);

        CollectionAssert.AreEqual(new[] { 1, 2, 1 }, chosen, "focus follows: Left from the third tab chooses the second");
    }
}
