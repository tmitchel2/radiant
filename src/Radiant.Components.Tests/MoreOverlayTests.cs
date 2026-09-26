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
public class MoreOverlayTests
{
    private static readonly Vector2 Viewport = new(800, 600);

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
        for (var i = 0; i < 30; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static IEnumerable<SemanticsNode> All(UIRoot root) => All(root.GetSemantics());

    private static SemanticsNode Find(UIRoot root, SemanticsRole role, string label) => All(root).Single(n => n.Role == role && n.Label == label);

    private static bool Showing(UIRoot root, SemanticsRole role) => All(root).Any(n => n.Role == role);

    private static Vector2 Centre(SemanticsNode node) => new(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);

    private static void Click(UIRoot root, Vector2 at, PointerButton button = PointerButton.Left)
    {
        root.PointerDown(at, button);
        root.PointerUp(at, button);
        Settle(root);
    }

    private static void Press(UIRoot root, KeyCode key, KeyModifiers modifiers = KeyModifiers.None)
    {
        root.KeyDown(key, modifiers);
        Settle(root);
    }

    private static Element Target(string label) => new Box
    {
        Focusable = true,
        Semantics = new Semantics { Role = SemanticsRole.Button, Label = label },
        Layout = new LayoutStyle { Width = 300, Height = 200 },
    };

    [TestMethod]
    public void ARightClickOpensTheMenuAtThePointer()
    {
        var chosen = new List<string>();
        using var root = Mount(new ContextMenu(Target("Canvas"), [new MenuItem("Cut", () => chosen.Add("cut")), new MenuItem("Paste", () => chosen.Add("paste"))]));

        Click(root, new Vector2(150, 100), PointerButton.Right);
        var menu = All(root).Single(n => n.Role == SemanticsRole.Menu).Bounds;
        Click(root, Centre(Find(root, SemanticsRole.MenuItem, "Paste")));

        Assert.AreEqual(150, menu.X, 2, "the menu's corner is at the pointer");
        Assert.AreEqual(100, menu.Y, 6);
        CollectionAssert.AreEqual(new[] { "paste" }, chosen);
        Assert.IsFalse(Showing(root, SemanticsRole.Menu), "a choice closes it");
    }

    [TestMethod]
    public void ALeftClickDoesNotOpenIt()
    {
        using var root = Mount(new ContextMenu(Target("Canvas"), [new MenuItem("Cut", () => { })]));

        Click(root, new Vector2(150, 100));

        Assert.IsFalse(Showing(root, SemanticsRole.Menu));
    }

    [TestMethod]
    public void ShiftF10OpensItFromTheKeyboardAndEscapeClosesIt()
    {
        using var root = Mount(new ContextMenu(Target("Canvas"), [new MenuItem("Cut", () => { })]));
        Click(root, new Vector2(150, 100));

        Press(root, KeyCode.F10, KeyModifiers.Shift);
        var opened = Showing(root, SemanticsRole.Menu);
        var focusedItem = Find(root, SemanticsRole.MenuItem, "Cut").IsFocused;
        Press(root, KeyCode.Escape);

        Assert.IsTrue(opened);
        Assert.IsTrue(focusedItem, "focus goes into the menu");
        Assert.IsFalse(Showing(root, SemanticsRole.Menu));
        Assert.IsTrue(Find(root, SemanticsRole.Button, "Canvas").IsFocused, "and comes back");
    }

    private sealed record PopoverHost(Signal<bool> Open) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var anchor = context.UseRef(new ElementRef()).Value;
            var open = Open;
            return new Box
            {
                Layout = new LayoutStyle { RowGap = 200 },
                Children =
                [
                    new Box { Ref = anchor, Children = [new SurfaceButton("Filters") { OnPress = () => open.Value = true }] },
                    new SurfaceButton("Elsewhere"),
                    new Popover(anchor, context.Watch(open), () => open.Value = false, new Checkbox(true, _ => { }) { Label = "In stock" }) { Label = "Filters panel" },
                ],
            };
        }
    }

    [TestMethod]
    public void APopoverOpensBesideItsAnchorAndClosesOnAnOutsidePress()
    {
        var open = new Signal<bool>(false);
        using var root = Mount(new PopoverHost(open));
        var button = Find(root, SemanticsRole.Button, "Filters").Bounds;

        Click(root, Centre(Find(root, SemanticsRole.Button, "Filters")));
        var panel = Find(root, SemanticsRole.Dialog, "Filters panel").Bounds;
        var focusInside = Find(root, SemanticsRole.CheckBox, "In stock").IsFocused;
        Click(root, Centre(Find(root, SemanticsRole.Button, "Elsewhere")));

        Assert.IsTrue(panel.Y >= button.Bottom, "below the button");
        Assert.AreEqual(button.X + button.Width / 2, panel.X + panel.Width / 2, 1.5f, "centred on it");
        Assert.IsTrue(focusInside);
        Assert.IsFalse(open.Value);
    }

    private static Element SheetOf(Signal<bool> open, SheetSide side = SheetSide.End) => new Host(ctx => new Sheet(ctx.Watch(open), () => open.Value = false, new TextField("Search"))
    {
        Title = "Filters",
        Side = side,
        Size = 320,
        Actions = [new SurfaceButton("Apply")],
    });

    [TestMethod]
    public void ASheetSlidesInAlongItsEdge()
    {
        using var end = Mount(SheetOf(new Signal<bool>(true)));
        using var bottom = Mount(SheetOf(new Signal<bool>(true), SheetSide.Bottom));

        var right = Find(end, SemanticsRole.Dialog, "Filters").Bounds;
        var low = Find(bottom, SemanticsRole.Dialog, "Filters").Bounds;

        Assert.AreEqual((800f - 320f, 320f, 600f), (right.X, right.Width, right.Height));
        Assert.AreEqual((600f - 320f, 800f), (low.Y, low.Width));
        Assert.IsNotNull(All(end).FirstOrDefault(n => n.Role == SemanticsRole.Heading && n.Label == "Filters"));
    }

    [TestMethod]
    public void ASheetClosesFromItsButtonEscapeOrTheScrim()
    {
        var open = new Signal<bool>(true);
        using var root = Mount(SheetOf(open));

        Click(root, Centre(Find(root, SemanticsRole.Button, "Close")));
        var byButton = !open.Value;
        open.Value = true;
        Settle(root);
        Press(root, KeyCode.Escape);
        var byEscape = !open.Value;
        open.Value = true;
        Settle(root);
        Click(root, new Vector2(100, 300));

        Assert.IsTrue(byButton && byEscape);
        Assert.IsFalse(open.Value, "a press on the scrim");
    }

    [TestMethod]
    public void AnAlertMustBeAnswered()
    {
        var open = new Signal<bool>(true);
        var answers = new List<string>();
        using var root = Mount(new Host(ctx => new AlertDialog(ctx.Watch(open), "Discard changes?", () =>
        {
            answers.Add("discard");
            open.Value = false;
        }, () =>
        {
            answers.Add("keep");
            open.Value = false;
        })
        {
            ConfirmText = "Discard",
            CancelText = "Keep editing",
            Destructive = true,
        }));

        Press(root, KeyCode.Escape);
        Click(root, new Vector2(10, 10));
        var stillOpen = Showing(root, SemanticsRole.Dialog);
        Click(root, Centre(Find(root, SemanticsRole.Button, "Discard")));

        Assert.IsTrue(stillOpen, "Escape and the scrim don't close it");
        CollectionAssert.AreEqual(new[] { "discard" }, answers);
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
