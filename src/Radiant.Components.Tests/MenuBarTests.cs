using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class MenuBarTests
{
    private static readonly Vector2 Viewport = new(700, 500);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box { Layout = new LayoutStyle { FlexGrow = 1, AlignItems = Align.FlexStart }, Children = [element] }));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root)
    {
        for (var i = 0; i < 20; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static IEnumerable<SemanticsNode> All(UIRoot root) => All(root.GetSemantics());

    private static Vector2 Centre(SemanticsNode node) => new(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);

    private static SemanticsNode Title(UIRoot root, string title) => All(root).Single(n => n.Role == SemanticsRole.Button && n.Label == title);

    private static string[] Items(UIRoot root) => [.. All(root).Where(n => n.Role == SemanticsRole.MenuItem).Select(n => n.Label!)];

    private static void Click(UIRoot root, SemanticsNode node)
    {
        root.PointerMove(Centre(node));
        root.PointerDown(Centre(node));
        root.PointerUp(Centre(node));
        Settle(root);
    }

    private static MenuBar Bar(List<string> done) => new(
    [
        new MenuBarMenu("File", [new MenuItem("New", () => done.Add("new")), new MenuItem("Open…", () => done.Add("open"))]),
        new MenuBarMenu("Edit", [new MenuItem("Undo", () => done.Add("undo"))]),
        new MenuBarMenu("View", [new MenuItem("Zoom in", () => done.Add("zoom"))]),
    ]);

    [TestMethod]
    public void APressOpensAMenuAndAChoiceRunsAndClosesIt()
    {
        var done = new List<string>();
        using var root = Mount(Bar(done));

        Click(root, Title(root, "File"));
        var file = Items(root);
        Click(root, All(root).Single(n => n.Label == "Open…" && n.Role == SemanticsRole.MenuItem));

        CollectionAssert.AreEqual(new[] { "New", "Open…" }, file);
        CollectionAssert.AreEqual(new[] { "open" }, done);
        Assert.AreEqual(0, Items(root).Length);
    }

    [TestMethod]
    public void WhileOneIsOpenThePointerMovesBetweenMenus()
    {
        using var root = Mount(Bar([]));
        Click(root, Title(root, "File"));

        root.PointerMove(Centre(Title(root, "View")));
        Settle(root);

        CollectionAssert.AreEqual(new[] { "Zoom in" }, Items(root));
        Assert.AreEqual(true, Title(root, "View").Semantics.Expanded);
    }

    [TestMethod]
    public void ThePointerAloneDoesNotOpenOne()
    {
        using var root = Mount(Bar([]));

        root.PointerMove(Centre(Title(root, "Edit")));
        Settle(root);

        Assert.AreEqual(0, Items(root).Length);
    }

    [TestMethod]
    public void LeftAndRightMoveBetweenMenusWrappingAround()
    {
        using var root = Mount(Bar([]));
        Click(root, Title(root, "File"));

        root.KeyDown(KeyCode.Right);
        Settle(root);
        var edit = Items(root);
        root.KeyDown(KeyCode.Left);
        Settle(root);
        root.KeyDown(KeyCode.Left);
        Settle(root);

        CollectionAssert.AreEqual(new[] { "Undo" }, edit);
        CollectionAssert.AreEqual(new[] { "Zoom in" }, Items(root), "left from File wraps to View");
    }
}
