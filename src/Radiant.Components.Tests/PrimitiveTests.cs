using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Components.Primitives;
using Radiant.Layout;
using Radiant.Scrolling;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class PrimitiveTests
{
    private static readonly Vector2 Viewport = new(400, 300);
    private static readonly RectangleF Anchor = new(100, 100, 80, 30);

    private static LayoutStyle At(float x, float y, float w, float h) => new()
    {
        Position = PositionType.Absolute,
        Inset = new Edges(x, y, Dimension.Undefined, Dimension.Undefined),
        Width = w,
        Height = h,
    };

    private static void Frames(UIRoot root, int count = 5)
    {
        root.Update(Viewport);
        for (var i = 0; i < count; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    [TestMethod]
    public void ContentGoesBelowItsAnchorAtTheStart()
    {
        var (position, side) = AnchoredPlacement.Place(Anchor, new Vector2(120, 60), Viewport, Side.Bottom, SideAlign.Start, 4, 8);

        Assert.AreEqual(new Vector2(100, 134), position);
        Assert.AreEqual(Side.Bottom, side);
    }

    [TestMethod]
    public void ContentFlipsAboveWhenThereIsNoRoomBelow()
    {
        var low = new RectangleF(100, 250, 80, 30);

        var (position, side) = AnchoredPlacement.Place(low, new Vector2(120, 60), Viewport, Side.Bottom, SideAlign.Start, 4, 8);

        Assert.AreEqual(Side.Top, side);
        Assert.AreEqual(250 - 4 - 60, position.Y);
    }

    [TestMethod]
    public void ContentShiftsToStayOnScreenAndCentres()
    {
        var nearRight = new RectangleF(360, 100, 30, 30);

        var (shifted, _) = AnchoredPlacement.Place(nearRight, new Vector2(120, 60), Viewport, Side.Bottom, SideAlign.Start, 4, 8);
        var (centred, _) = AnchoredPlacement.Place(Anchor, new Vector2(40, 20), Viewport, Side.Bottom, SideAlign.Center, 4, 8);
        var (right, _) = AnchoredPlacement.Place(Anchor, new Vector2(40, 20), Viewport, Side.Right, SideAlign.Center, 4, 8);

        Assert.AreEqual(400 - 8 - 120, shifted.X);
        Assert.AreEqual(120, centred.X);
        Assert.AreEqual(new Vector2(184, 105), right);
    }

    [TestMethod]
    public void AnAnchoredPopoverIsPlacedUnderItsAnchorAndFollowsItWhenScrolled()
    {
        var anchor = new ElementRef();
        var content = new ElementRef();
        var scroll = new ScrollController(new ScrollBehaviour());
        using var root = new UIRoot(new ScrollArea
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Controller = scroll,
            Children =
            [
                new Box { Layout = new LayoutStyle { Height = 100 } },
                new Box { Ref = anchor, Layout = new LayoutStyle { Height = 30, Width = 80 } },
                new Box { Layout = new LayoutStyle { Height = 600 } },
                new Anchored(anchor, new Box { Ref = content, Layout = new LayoutStyle { Width = 120, Height = 60 } }),
            ],
        });
        Frames(root);

        Assert.AreEqual(new RectangleF(8, 134, 120, 60), content.Bounds, "shifted off the edge by the padding");

        root.Wheel(new Vector2(50, 50), new Vector2(0, 40));
        Frames(root, 30);

        Assert.AreEqual(94, content.Bounds.Y, 0.5f);
    }

    [TestMethod]
    public void APressOutsideTheLayerDismissesItButOneInsideDoesNot()
    {
        var dismissed = 0;
        using var root = new UIRoot(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children = [new DismissableLayer(new Box { Layout = At(10, 10, 50, 50) }, () => dismissed++)],
        });
        Frames(root);

        root.PointerDown(new Vector2(20, 20));
        root.PointerDown(new Vector2(200, 200));

        Assert.AreEqual(1, dismissed);
    }

    [TestMethod]
    public void PressesOnContentTheLayerShowsInAPortalCountAsInside()
    {
        var dismissed = 0;
        using var root = new UIRoot(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children = [new DismissableLayer(new Portal(new Box { Layout = At(200, 200, 50, 50) }), () => dismissed++)],
        });
        Frames(root);

        root.PointerDown(new Vector2(210, 210));

        Assert.AreEqual(0, dismissed);
    }

    [TestMethod]
    public void EscapeInsideTheLayerDismissesIt()
    {
        var dismissed = 0;
        using var root = new UIRoot(new DismissableLayer(new Box { Focusable = true }, () => dismissed++));
        Frames(root);
        root.MoveFocus(forward: true);

        root.KeyDown(KeyCode.Escape);

        Assert.AreEqual(1, dismissed);
    }

    [TestMethod]
    public void AFocusScopeTakesFocusTrapsTabAndGivesFocusBack()
    {
        var focused = new System.Collections.Generic.List<string>();
        Box Field(string name) => new() { Focusable = true, OnFocus = _ => focused.Add(name) };
        var open = new UI.Core.Signal<bool>(false);
        using var root = new UIRoot(new Box
        {
            Children =
            [
                Field("outside"),
                new Lambda(ctx => ctx.Watch(open) ? new FocusScope(new Box { Children = [Field("first"), Field("second")] }) : null),
            ],
        });
        Frames(root);
        root.MoveFocus(forward: true);

        open.Value = true;
        Frames(root);
        root.KeyDown(KeyCode.Tab);
        root.KeyDown(KeyCode.Tab);
        open.Value = false;
        Frames(root);

        CollectionAssert.AreEqual(new[] { "outside", "first", "second", "first", "outside" }, focused);
    }

    private sealed record Lambda(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
