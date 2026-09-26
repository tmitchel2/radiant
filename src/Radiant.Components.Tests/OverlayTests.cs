using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class OverlayTests
{
    private static readonly Vector2 Viewport = new(800, 600);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), element));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root, int frames = 40)
    {
        root.Update(Viewport);
        for (var i = 0; i < frames; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static SemanticsNode? Find(UIRoot root, SemanticsRole role, string? label = null) =>
        All(root.GetSemantics()).FirstOrDefault(n => n.Role == role && (label is null || n.Label == label));

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }

    [TestMethod]
    public void AnOpenDialogTakesFocusAndClosesOnEscape()
    {
        var open = new UI.Core.Signal<bool>(true);
        using var root = Mount(new Host(ctx => new Dialog(ctx.Watch(open), () => open.Value = false)
        {
            Title = "Delete?",
            Actions = [new SurfaceButton("Cancel", ButtonVariant.Text)],
        }));

        Assert.IsNotNull(Find(root, SemanticsRole.Dialog, "Delete?"));
        Assert.IsTrue(Find(root, SemanticsRole.Button, "Cancel")!.IsFocused);

        root.KeyDown(KeyCode.Escape);
        Settle(root);

        Assert.IsFalse(open.Value);
        Assert.IsNull(Find(root, SemanticsRole.Dialog));
    }

    [TestMethod]
    public void PressingTheScrimClosesADialogUnlessItMustBeAnswered()
    {
        var closed = 0;
        using var dismissible = Mount(new Dialog(true, () => closed++) { Title = "A" });
        using var modal = Mount(new Dialog(true, () => closed += 10) { Title = "B", Dismissible = false });

        dismissible.PointerDown(new Vector2(5, 5));
        modal.PointerDown(new Vector2(5, 5));
        modal.KeyDown(KeyCode.Escape);

        Assert.AreEqual(1, closed);
    }

    [TestMethod]
    public void AMenuMovesWithArrowsAndChoosingClosesIt()
    {
        var anchor = new ElementRef();
        var open = new UI.Core.Signal<bool>(true);
        var chosen = new List<string>();
        using var root = Mount(new Host(ctx => new Box
        {
            Children =
            [
                new Box { Ref = anchor, Layout = new Radiant.Layout.LayoutStyle { Width = 40, Height = 40 } },
                new Menu(anchor, ctx.Watch(open), () => open.Value = false,
                [
                    new MenuItem("Copy", () => chosen.Add("Copy")),
                    new MenuItem("Paste", () => chosen.Add("Paste")),
                ]),
            ],
        }));

        Assert.IsTrue(Find(root, SemanticsRole.MenuItem, "Copy")!.IsFocused);
        root.KeyDown(KeyCode.Down);
        Assert.IsTrue(Find(root, SemanticsRole.MenuItem, "Paste")!.IsFocused);
        root.KeyDown(KeyCode.Enter);
        Settle(root);

        CollectionAssert.AreEqual(new[] { "Paste" }, chosen);
        Assert.IsFalse(open.Value);
        Assert.IsNull(Find(root, SemanticsRole.Menu));
    }

    [TestMethod]
    public void AMenuOpensUnderItsAnchor()
    {
        var anchor = new ElementRef();
        using var root = Mount(new Box
        {
            Layout = new Radiant.Layout.LayoutStyle { Padding = Radiant.Layout.Edges.All(50) },
            Children =
            [
                new Box { Ref = anchor, Layout = new Radiant.Layout.LayoutStyle { Width = 40, Height = 40 } },
                new Menu(anchor, true, () => { }, [new MenuItem("Copy")]),
            ],
        });

        var menu = Find(root, SemanticsRole.Menu)!;

        Assert.AreEqual(50, menu.Bounds.X, 0.5f);
        Assert.AreEqual(94, menu.Bounds.Y, 0.5f);
    }

    [TestMethod]
    public void ATooltipAppearsAfterThePointerRestsAndGoesWhenItLeaves()
    {
        using var root = Mount(new Box
        {
            Layout = new Radiant.Layout.LayoutStyle { Padding = Radiant.Layout.Edges.All(100) },
            Children = [new Tooltip("Search the web", new IconButton("search", "Search"))],
        });
        var button = Find(root, SemanticsRole.Button, "Search")!;
        var centre = new Vector2(button.Bounds.X + 20, button.Bounds.Y + 20);

        root.PointerMove(centre);
        Settle(root, frames: 20);
        Assert.IsNull(Find(root, SemanticsRole.Tooltip), "not yet: 333 ms of the 600");

        Settle(root, frames: 30);
        Assert.IsNotNull(Find(root, SemanticsRole.Tooltip));

        root.PointerMove(new Vector2(700, 500));
        Settle(root);
        Assert.IsNull(Find(root, SemanticsRole.Tooltip));
    }

    [TestMethod]
    public void APressableListItemIsAListItemToAssistiveTechnology()
    {
        var pressed = 0;
        using var root = Mount(new ListItem("Inbox") { SupportingText = "24 unread", OnPress = () => pressed++ });

        var item = Find(root, SemanticsRole.ListItem)!;
        root.PointerDown(new Vector2(item.Bounds.X + 5, item.Bounds.Y + 5));
        root.PointerUp(new Vector2(item.Bounds.X + 5, item.Bounds.Y + 5));

        Assert.AreEqual("Inbox 24 unread", item.Label);
        Assert.AreEqual(72, item.Bounds.Height, 0.5f, "two lines");
        Assert.AreEqual(1, pressed);
    }
}
