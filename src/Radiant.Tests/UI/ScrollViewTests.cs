using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Radiant.Input;
using Radiant.Scrolling;
using Radiant.UI;
using Silk.NET.Input;

namespace Radiant.Tests.UI;

[TestClass]
public class ScrollViewTests
{
    // A leaf element that records the pointer position it was updated with.
    private sealed class MouseProbe : UIElement
    {
        public Vector2 SeenMouse { get; private set; }
        public override void Update(InputState input, double deltaTime) => SeenMouse = input.MousePosition;
        public override void Draw(Renderer2D renderer) { }
    }

    private static ScrollView MakeScrollable(out MouseProbe probe)
    {
        var sv = new ScrollView(new ScrollBehaviour { Overscroll = OverscrollMode.Clamp, WheelStep = 28f })
        {
            Position = Vector2.Zero,
            Size = new Vector2(100, 100),
        };
        probe = sv.Add(new MouseProbe { Position = Vector2.Zero, Size = new Vector2(100, 500) });
        return sv;
    }

    [TestMethod]
    public void WheelOverScrollableContentScrolls()
    {
        var sv = MakeScrollable(out _);
        var input = new InputState { MousePosition = new Vector2(50, 10), ScrollDelta = new Vector2(0, -2) };
        sv.Update(input, 1.0 / 60.0);

        Assert.AreEqual(56f, sv.Controller.Offset.Y, 0.01f, "2 notches * 28 wheel step");
    }

    [TestMethod]
    public void ChildrenAreHitTestedInContentSpace()
    {
        var sv = MakeScrollable(out var probe);
        // Frame 1: wheel applies (offset becomes 56) after children are updated.
        var f1 = new InputState { MousePosition = new Vector2(50, 10), ScrollDelta = new Vector2(0, -2) };
        sv.Update(f1, 1.0 / 60.0);
        // Frame 2: children are now hit-tested against the applied offset (screen + offset).
        var f2 = new InputState { MousePosition = new Vector2(50, 10) };
        sv.Update(f2, 1.0 / 60.0);

        Assert.AreEqual(new Vector2(50, 66), probe.SeenMouse);
    }

    [TestMethod]
    public void RestoresPointerAfterUpdatingChildren()
    {
        var sv = MakeScrollable(out _);
        var input = new InputState { MousePosition = new Vector2(50, 10), ScrollDelta = new Vector2(0, -2) };
        sv.Update(input, 1.0 / 60.0);

        Assert.AreEqual(new Vector2(50, 10), input.MousePosition, "shared InputState must be left untouched");
    }

    [TestMethod]
    public void CapturesInputWhenHoveringScrollableContent()
    {
        var sv = MakeScrollable(out _);
        var input = new InputState { MousePosition = new Vector2(50, 10) };
        sv.Update(input, 1.0 / 60.0);
        Assert.IsTrue(sv.IsCapturingInput);
    }

    [TestMethod]
    public void DoesNotCaptureWhenContentFits()
    {
        var sv = new ScrollView { Position = Vector2.Zero, Size = new Vector2(100, 100) };
        sv.Add(new MouseProbe { Position = Vector2.Zero, Size = new Vector2(100, 40) }); // fits
        var input = new InputState { MousePosition = new Vector2(50, 10) };
        sv.Update(input, 1.0 / 60.0);
        Assert.IsFalse(sv.IsCapturingInput);
    }

    [TestMethod]
    public void DoesNotCaptureWhenPointerOutside()
    {
        var sv = MakeScrollable(out _);
        var input = new InputState { MousePosition = new Vector2(500, 500) };
        sv.Update(input, 1.0 / 60.0);
        Assert.IsFalse(sv.IsCapturingInput);
    }

    [TestMethod]
    public void DragToPanScrollsContent()
    {
        var sv = MakeScrollable(out _);
        var input = new InputState();
        input.SetMouseButton(MouseButton.Left, true);
        input.MousePosition = new Vector2(50, 50);
        sv.Update(input, 1.0 / 60.0); // press → pan possible

        input.EndFrame();             // clears pressed + delta; Left stays down
        input.MousePosition = new Vector2(50, 50);
        input.MouseDelta = new Vector2(0, -30); // drag up → scroll toward content end
        sv.Update(input, 1.0 / 60.0);

        Assert.IsTrue(sv.Controller.Offset.Y > 10f, "drag-to-pan should move the offset");
    }

    [TestMethod]
    public void PressInsideCapturesInput()
    {
        var sv = MakeScrollable(out _);
        var input = new InputState();
        input.SetMouseButton(MouseButton.Left, true);
        input.MousePosition = new Vector2(50, 50);
        sv.Update(input, 1.0 / 60.0);

        Assert.IsTrue(sv.IsCapturingInput, "a press inside the scroll body claims input");
    }
}
