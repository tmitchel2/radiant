using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Radiant.Input;
using Radiant.Scrolling;
using Radiant.UI;
using Silk.NET.Input;

namespace Radiant.Tests.UI;

[TestClass]
public class ScrollViewInteractionTests
{
    private const double Dt = 1.0 / 60.0;

    private sealed class Spacer : UIElement
    {
        public override void Draw(Renderer2D renderer) { }
    }

    private static ScrollView Scrollable(float viewport, float content, ScrollBehaviour? b = null)
    {
        var sv = new ScrollView(b ?? new ScrollBehaviour { Overscroll = OverscrollMode.Clamp })
        {
            Position = Vector2.Zero,
            Size = new Vector2(viewport, viewport),
        };
        sv.Add(new Spacer { Position = Vector2.Zero, Size = new Vector2(viewport, content) });
        return sv;
    }

    private static void Settle(ScrollView sv)
    {
        for (var i = 0; i < 2000 && sv.IsAnimating; i++)
            sv.Update(new InputState { MousePosition = new Vector2(-1, -1) }, Dt);
    }

    [TestMethod]
    public void PageDownScrollsByViewport()
    {
        var sv = Scrollable(100, 1000);
        var input = new InputState { MousePosition = new Vector2(50, 50) };
        input.SetKey(Key.PageDown, true);
        sv.Update(input, Dt);
        Settle(sv);

        Assert.AreEqual(100f, sv.Controller.Offset.Y, 0.5f, "PageDown should advance one viewport");
    }

    [TestMethod]
    public void EndAndHomeKeysJumpToExtremes()
    {
        var sv = Scrollable(100, 1000);
        var end = new InputState { MousePosition = new Vector2(50, 50) };
        end.SetKey(Key.End, true);
        sv.Update(end, Dt);
        Settle(sv);
        Assert.AreEqual(900f, sv.Controller.Offset.Y, 0.5f, "End → max offset");

        var home = new InputState { MousePosition = new Vector2(50, 50) };
        home.SetKey(Key.Home, true);
        sv.Update(home, Dt);
        Settle(sv);
        Assert.AreEqual(0f, sv.Controller.Offset.Y, 0.5f, "Home → origin");
    }

    [TestMethod]
    public void DraggingThumbScrollsProportionally()
    {
        var sv = Scrollable(100, 500); // max 400, thumb height 20, track travel 80
        var press = new InputState();
        press.SetMouseButton(MouseButton.Left, true);
        press.MousePosition = new Vector2(97, 5); // on the thumb (track x∈[94,100], thumb y∈[0,20])
        sv.Update(press, Dt);

        var drag = new InputState();
        drag.SetMouseButton(MouseButton.Left, true);
        drag.MousePosition = new Vector2(97, 45);
        drag.MouseDelta = new Vector2(0, 40); // drag thumb down 40px
        sv.Update(drag, Dt);

        // 40px thumb travel maps to 40 * (400/80) = 200 content units.
        Assert.AreEqual(200f, sv.Controller.Offset.Y, 1f);
    }

    [TestMethod]
    public void NestedInnerConsumesWheelOuterStays()
    {
        var outer = new ScrollView(new ScrollBehaviour { Overscroll = OverscrollMode.Clamp })
        {
            Position = Vector2.Zero,
            Size = new Vector2(200, 200),
        };
        var inner = new ScrollView(new ScrollBehaviour { Overscroll = OverscrollMode.Clamp })
        {
            Position = Vector2.Zero,
            Size = new Vector2(200, 100),
        };
        inner.Add(new Spacer { Position = Vector2.Zero, Size = new Vector2(200, 500) });
        outer.Add(inner);
        outer.Add(new Spacer { Position = new Vector2(0, 100), Size = new Vector2(200, 300) }); // outer scrollable

        var input = new InputState { MousePosition = new Vector2(100, 50), ScrollDelta = new Vector2(0, -2) };
        outer.Update(input, Dt);

        Assert.AreEqual(56f, inner.Controller.Offset.Y, 0.5f, "inner consumes the wheel");
        Assert.AreEqual(0f, outer.Controller.Offset.Y, 0.5f, "outer does not also scroll");
    }

    [TestMethod]
    public void NestedWheelHandsOffToOuterAtInnerBoundary()
    {
        var outer = new ScrollView(new ScrollBehaviour { Overscroll = OverscrollMode.Clamp })
        {
            Position = Vector2.Zero,
            Size = new Vector2(200, 200),
        };
        var inner = new ScrollView(new ScrollBehaviour { Overscroll = OverscrollMode.Clamp })
        {
            Position = Vector2.Zero,
            Size = new Vector2(200, 100),
        };
        inner.Add(new Spacer { Position = Vector2.Zero, Size = new Vector2(200, 500) });
        outer.Add(inner);
        outer.Add(new Spacer { Position = new Vector2(0, 100), Size = new Vector2(200, 300) });

        // Outer scrolled down a bit; inner is at its top. Wheel up over the inner.
        outer.ScrollTo(new Vector2(0, 50), animated: false);
        var input = new InputState { MousePosition = new Vector2(100, 50), ScrollDelta = new Vector2(0, 2) };
        outer.Update(input, Dt);

        Assert.AreEqual(0f, inner.Controller.Offset.Y, 0.5f, "inner at top cannot scroll up");
        Assert.IsTrue(outer.Controller.Offset.Y < 50f, "the unconsumed wheel hands off to the outer scroller");
    }
}
