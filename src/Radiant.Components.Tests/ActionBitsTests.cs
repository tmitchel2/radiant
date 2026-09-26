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
public class ActionBitsTests
{
    private static readonly Vector2 Viewport = new(900, 500);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(20), AlignItems = Align.FlexStart, RowGap = 20 },
            Children = [new SurfaceButton("Before"), element, new SurfaceButton("After")],
        }));
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

    private static SemanticsNode Find(UIRoot root, SemanticsRole role, string label) => All(root).Single(n => n.Role == role && n.Label == label);

    private static string? Focused(UIRoot root) => All(root).SingleOrDefault(n => n.IsFocused)?.Label;

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
    public void AnAlertIsAnnouncedAndCanBeDismissed()
    {
        var dismissed = 0;
        using var root = Mount(new Alert("Payment failed") { Kind = AlertKind.Error, Text = "Your card was declined.", OnDismiss = () => dismissed++ });

        var alert = Find(root, SemanticsRole.Alert, "Payment failed");
        Click(root, Find(root, SemanticsRole.Button, "Dismiss"));

        Assert.AreEqual("Your card was declined.", alert.Semantics.Description);
        Assert.AreEqual(1, dismissed);
    }

    [TestMethod]
    public void AnExtendedFabShowsItsLabel()
    {
        using var small = Mount(new Fab("edit", "Compose"));
        using var wide = Mount(new Fab("edit", "Compose") { Extended = true });

        var icon = Find(small, SemanticsRole.Button, "Compose").Bounds;
        var extended = Find(wide, SemanticsRole.Button, "Compose").Bounds;

        Assert.AreEqual((56f, 56f), (icon.Width, icon.Height));
        Assert.IsTrue(extended.Width > 100, $"{extended.Width}");
    }

    [TestMethod]
    public void AOneOfGroupKeepsExactlyOneOn()
    {
        var selected = new Signal<IReadOnlySet<int>>(new HashSet<int> { 0 });
        (string, string)[] items = [("format_align_left", "Left"), ("format_align_center", "Centre"), ("format_align_right", "Right")];
        using var root = Mount(new Host(ctx => new ToggleGroup(items, ctx.Watch(selected), s => selected.Value = s)));

        Click(root, Find(root, SemanticsRole.Button, "Right"));
        var moved = selected.Value.ToArray();
        Click(root, Find(root, SemanticsRole.Button, "Right"));

        CollectionAssert.AreEqual(new[] { 2 }, moved);
        CollectionAssert.AreEqual(new[] { 2 }, selected.Value.ToArray(), "pressing the one that's on keeps it on");
        Assert.IsTrue(Find(root, SemanticsRole.Button, "Right").Semantics.Selected);
    }

    [TestMethod]
    public void AMultipleGroupTogglesEachButton()
    {
        var selected = new Signal<IReadOnlySet<int>>(new HashSet<int>());
        (string, string)[] items = [("format_bold", "Bold"), ("format_italic", "Italic")];
        using var root = Mount(new Host(ctx => new ToggleGroup(items, ctx.Watch(selected), s => selected.Value = s) { Multiple = true }));

        Click(root, Find(root, SemanticsRole.Button, "Bold"));
        Click(root, Find(root, SemanticsRole.Button, "Italic"));
        Click(root, Find(root, SemanticsRole.Button, "Bold"));

        CollectionAssert.AreEqual(new[] { 1 }, selected.Value.ToArray());
    }

    [TestMethod]
    public void ArrowsMoveWithinAToolbarAndStopAtItsEnds()
    {
        using var root = Mount(new Toolbar([new IconButton("undo", "Undo"), new IconButton("redo", "Redo"), new Divider { Vertical = true }, new IconButton("format_bold", "Bold")]) { Label = "Formatting" });
        Click(root, Find(root, SemanticsRole.Button, "Undo"));

        Press(root, KeyCode.Right);
        var second = Focused(root);
        Press(root, KeyCode.End);
        var last = Focused(root);
        Press(root, KeyCode.Right);
        var stayed = Focused(root);
        Press(root, KeyCode.Home);

        Assert.AreEqual("Redo", second);
        Assert.AreEqual("Bold", last);
        Assert.AreEqual("Bold", stayed, "not out to the button after the bar");
        Assert.AreEqual("Undo", Focused(root));
    }

    [TestMethod]
    public void ASplitButtonsArrowOffersTheAlternatives()
    {
        var done = new List<string>();
        using var root = Mount(new SplitButton("Save", () => done.Add("save"), [new MenuItem("Save as…", () => done.Add("save as")), new MenuItem("Save all", () => done.Add("save all"))]));

        Click(root, Find(root, SemanticsRole.Button, "Save"));
        Click(root, Find(root, SemanticsRole.Button, "More Save options"));
        Click(root, Find(root, SemanticsRole.MenuItem, "Save all"));

        CollectionAssert.AreEqual(new[] { "save", "save all" }, done);
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
