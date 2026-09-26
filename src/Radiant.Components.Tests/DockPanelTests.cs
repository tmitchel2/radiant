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
public class DockPanelTests
{
    private static readonly Vector2 Viewport = new(1000, 600);

    private static readonly DockLayout Initial = new()
    {
        Left = new DockGroup(["files", "outline"], Size: 200),
        Center = new DockGroup(["editor"]),
        Right = new DockGroup(["properties"], Size: 200),
    };

    private static DockItem[] Items =>
    [
        new("files", "Files", new SurfaceText("Files content")),
        new("outline", "Outline", new SurfaceText("Outline content")),
        new("editor", "Editor", new SurfaceText("Editor content")) { Closable = false },
        new("properties", "Properties", new SurfaceText("Properties content")),
    ];

    private static UIRoot Mount(Signal<DockLayout> layout)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children = [new Host(context => new DockPanel(context.Watch(layout), l => layout.Value = l, Items))],
        }));
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

    private static SemanticsNode Tab(UIRoot root, string title) => All(root.GetSemantics()).Single(n => n.Role == SemanticsRole.Tab && n.Label == title);

    private static SemanticsNode TabList(UIRoot root, DockArea area) => All(root.GetSemantics()).Single(n => n.Role == SemanticsRole.TabList && n.Label == $"{area} panels");

    private static Vector2 Centre(SemanticsNode node) => new(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);

    private static bool Shows(UIRoot root, string text) => All(root.GetSemantics()).Any(n => n.Label == text);

    private static void Drag(UIRoot root, Vector2 from, Vector2 to)
    {
        root.PointerDown(from);
        root.PointerMove(from + new Vector2(10, 10));
        Settle(root);
        root.PointerMove(to);
        Settle(root);
        root.PointerUp(to);
        Settle(root);
    }

    [TestMethod]
    public void TheLayoutMovesClosesAndActivatesPanels()
    {
        var moved = Initial.Move("files", DockArea.Right);
        Assert.AreEqual(DockArea.Right, moved.AreaOf("files"));
        CollectionAssert.AreEqual(new[] { "properties", "files" }, moved.Right.Items.ToArray());
        Assert.AreEqual("files", moved.Right.Shown);
        Assert.AreEqual("outline", moved.Left.Shown, "the neighbour is shown in its place");

        var closed = Initial.Close("properties");
        Assert.IsNull(closed.AreaOf("properties"));
        Assert.AreEqual(0, closed.Right.Items.Count);

        Assert.AreEqual("outline", Initial.Activate("outline").Left.Shown);
        Assert.AreEqual(DockArea.Bottom, closed.Move("properties", DockArea.Bottom).AreaOf("properties"), "moving a closed panel shows it again");
        Assert.AreEqual(300f, Initial.Resize(DockArea.Left, 300).Left.Size);
    }

    [TestMethod]
    public void EachAreaShowsItsPanelsAsTabsAndTheChosenOnesContent()
    {
        var layout = new Signal<DockLayout>(Initial);
        using var root = Mount(layout);

        Assert.IsTrue(Tab(root, "Files").Semantics.Selected);
        Assert.IsTrue(Shows(root, "Files content") && Shows(root, "Editor content") && Shows(root, "Properties content"));
        Assert.IsFalse(Shows(root, "Outline content"));
        Assert.IsFalse(All(root.GetSemantics()).Any(n => n.Label == "Bottom panels"), "an empty area isn't shown");

        var outline = Centre(Tab(root, "Outline"));
        root.PointerDown(outline);
        root.PointerUp(outline);
        Settle(root);

        Assert.AreEqual("outline", layout.Value.Left.Shown);
        Assert.IsTrue(Shows(root, "Outline content"));
        Assert.IsFalse(Shows(root, "Files content"));
    }

    [TestMethod]
    public void DraggingATabOntoAnotherAreaMovesIt()
    {
        var layout = new Signal<DockLayout>(Initial);
        using var root = Mount(layout);

        Drag(root, Centre(Tab(root, "Outline")), Centre(TabList(root, DockArea.Right)) + new Vector2(0, 100));

        Assert.AreEqual(DockArea.Right, layout.Value.AreaOf("outline"));
        Assert.IsTrue(Tab(root, "Outline").Semantics.Selected);
        Assert.IsTrue(Shows(root, "Outline content"));
    }

    [TestMethod]
    public void WhileDraggedTheTabsTitleFollowsThePointer()
    {
        var layout = new Signal<DockLayout>(Initial);
        using var root = Mount(layout);
        var from = Centre(Tab(root, "Outline"));

        root.PointerDown(from);
        root.PointerMove(from + new Vector2(200, 150));
        Settle(root);

        // Its title, apart from the tab's own: under the pointer, well below the tab strip.
        var ghost = All(root.GetSemantics()).Where(n => n.Label == "Outline" && n.Bounds.Y > from.Y + 50).ToArray();
        Assert.AreEqual(1, ghost.Length);
        Assert.AreEqual(from.X + 212, ghost[0].Bounds.X, 16f);
        root.PointerUp(from + new Vector2(200, 150));
        Settle(root);
        Assert.IsFalse(All(root.GetSemantics()).Any(n => n.Label == "Outline" && n.Bounds.Y > from.Y + 50), "it goes when dropped");
    }

    [TestMethod]
    public void DroppingNearTheBottomEdgeDocksThereEvenWhenItsEmpty()
    {
        var layout = new Signal<DockLayout>(Initial);
        using var root = Mount(layout);

        Drag(root, Centre(Tab(root, "Properties")), new Vector2(500, Viewport.Y - 10));

        Assert.AreEqual(DockArea.Bottom, layout.Value.AreaOf("properties"));
        Assert.IsTrue(All(root.GetSemantics()).Any(n => n.Label == "Bottom panels"));
        Assert.IsFalse(All(root.GetSemantics()).Any(n => n.Label == "Right panels"), "the emptied area goes");
    }

    [TestMethod]
    public void ATabsCloseButtonClosesItAndTheEditorCantBeClosed()
    {
        var layout = new Signal<DockLayout>(Initial);
        using var root = Mount(layout);

        var close = All(root.GetSemantics()).Single(n => n.Label == "Close Files");
        root.PointerDown(Centre(close));
        root.PointerUp(Centre(close));
        Settle(root);

        Assert.IsNull(layout.Value.AreaOf("files"));
        Assert.AreEqual("outline", layout.Value.Left.Shown);
        Assert.IsFalse(All(root.GetSemantics()).Any(n => n.Label == "Close Editor"));
    }

    [TestMethod]
    public void ATabsMenuMovesIt()
    {
        var layout = new Signal<DockLayout>(Initial);
        using var root = Mount(layout);

        root.PointerDown(Centre(Tab(root, "Files")), PointerButton.Right);
        Settle(root);
        var moveToBottom = All(root.GetSemantics()).Single(n => n.Role == SemanticsRole.MenuItem && n.Label == "Move to bottom");
        root.PointerDown(Centre(moveToBottom));
        root.PointerUp(Centre(moveToBottom));
        Settle(root);

        Assert.AreEqual(DockArea.Bottom, layout.Value.AreaOf("files"));
    }

    [TestMethod]
    public void TheDividersResizeTheAreas()
    {
        var layout = new Signal<DockLayout>(Initial);
        using var root = Mount(layout);

        var divider = Centre(All(root.GetSemantics()).Single(n => n.Role == SemanticsRole.Separator && n.Label == "Left panels"));
        root.PointerDown(divider);
        root.PointerMove(divider + new Vector2(50, 0));
        root.PointerUp(divider + new Vector2(50, 0));
        Settle(root);

        Assert.AreEqual(250f, layout.Value.Left.Size, 0.5f);
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
