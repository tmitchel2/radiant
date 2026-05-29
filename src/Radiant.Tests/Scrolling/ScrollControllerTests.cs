using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Scrolling;

namespace Radiant.Tests.Scrolling;

[TestClass]
public class ScrollControllerTests
{
    private const double Dt = 1.0 / 60.0;

    private static ScrollController Make(ScrollBehaviour? b = null, float viewport = 100f, float content = 1000f)
    {
        var c = new ScrollController(b ?? new ScrollBehaviour { Overscroll = OverscrollMode.Clamp });
        c.SetExtents(new Vector2(viewport, viewport), new Vector2(content, content));
        return c;
    }

    [TestMethod]
    public void WheelScrollClampsToContentExtent()
    {
        var c = Make();
        for (var i = 0; i < 20; i++) c.ApplyWheel(new Vector2(0, -100)); // scroll toward end
        Assert.AreEqual(900f, c.Offset.Y, 0.01f, "should clamp at MaxOffset = content - viewport");

        for (var i = 0; i < 20; i++) c.ApplyWheel(new Vector2(0, 100)); // scroll back to start
        Assert.AreEqual(0f, c.Offset.Y, 0.01f);
    }

    [TestMethod]
    public void WheelStepMovesByConfiguredAmount()
    {
        var c = Make(new ScrollBehaviour { WheelStep = 28f, Overscroll = OverscrollMode.Clamp });
        c.ApplyWheel(new Vector2(0, -1));
        Assert.AreEqual(28f, c.Offset.Y, 0.01f);
    }

    [TestMethod]
    public void MomentumGlidesForwardThenComesToRestInBounds()
    {
        var c = Make(new ScrollBehaviour { Overscroll = OverscrollMode.Clamp });
        c.BeginDrag();
        for (var i = 0; i < 3; i++) c.Drag(new Vector2(0, -20), Dt); // fling toward content end
        var atRelease = c.Offset.Y;
        c.EndDrag();

        Assert.IsTrue(c.IsAnimating, "a fling should enter momentum");

        for (var i = 0; i < 2000 && c.IsAnimating; i++) c.Update(Dt);

        Assert.IsFalse(c.IsAnimating, "momentum should come to rest");
        Assert.IsTrue(c.Offset.Y > atRelease, "momentum should continue past the release point");
        Assert.IsTrue(c.Offset.Y is >= 0f and <= 900f, "must rest within bounds");
    }

    [TestMethod]
    public void BounceOvershootsThenSettlesToEdge()
    {
        var c = Make(new ScrollBehaviour { Overscroll = OverscrollMode.Bounce });
        c.ScrollTo(new Vector2(0, 900), animated: false); // start at the bottom edge
        c.BeginDrag();
        c.Drag(new Vector2(0, -50), Dt); // drag further past the end
        Assert.IsTrue(c.Offset.Y > 900f, "rubber-band should stretch past the edge during drag");

        c.EndDrag();
        for (var i = 0; i < 2000 && c.IsAnimating; i++) c.Update(Dt);

        Assert.AreEqual(900f, c.Offset.Y, 0.1f, "should spring back exactly to the edge");
    }

    [TestMethod]
    public void ClampModeDoesNotOverscroll()
    {
        var c = Make(new ScrollBehaviour { Overscroll = OverscrollMode.Clamp });
        c.ScrollTo(new Vector2(0, 900), animated: false);
        c.BeginDrag();
        c.Drag(new Vector2(0, -200), Dt);
        Assert.AreEqual(900f, c.Offset.Y, 0.01f, "clamp mode must not exceed the edge");
    }

    [TestMethod]
    public void SnapToIntervalLandsOnNearestInterval()
    {
        var c = Make(new ScrollBehaviour
        {
            Overscroll = OverscrollMode.Clamp,
            Snap = new SnapConfig { Interval = 100f, DisableIntervalMomentum = true },
        });
        c.BeginDrag();
        c.Drag(new Vector2(0, -170), Dt); // land at offset 170
        c.EndDrag();
        for (var i = 0; i < 2000 && c.IsAnimating; i++) c.Update(Dt);

        Assert.AreEqual(200f, c.Offset.Y, 0.1f, "170 should snap up to the 200 interval");
    }

    [TestMethod]
    public void AnimatedScrollToConvergesOnTarget()
    {
        var c = Make(new ScrollBehaviour { Overscroll = OverscrollMode.Clamp });
        c.ScrollTo(new Vector2(0, 450), animated: true);
        Assert.IsTrue(c.IsAnimating);
        for (var i = 0; i < 2000 && c.IsAnimating; i++) c.Update(Dt);

        Assert.AreEqual(450f, c.Offset.Y, 0.1f);
        Assert.IsFalse(c.IsAnimating);
    }

    [TestMethod]
    public void NonAnimatedScrollToJumpsImmediately()
    {
        var c = Make();
        c.ScrollTo(new Vector2(0, 300), animated: false);
        Assert.AreEqual(300f, c.Offset.Y, 0.001f);
        Assert.IsFalse(c.IsAnimating);
    }

    [TestMethod]
    public void IsAnimatingTogglesAcrossMomentum()
    {
        var c = Make(new ScrollBehaviour { Overscroll = OverscrollMode.Clamp });
        Assert.IsFalse(c.IsAnimating, "idle at construction");
        c.BeginDrag();
        for (var i = 0; i < 3; i++) c.Drag(new Vector2(0, -25), Dt);
        c.EndDrag();
        Assert.IsTrue(c.IsAnimating, "animating during momentum");
        for (var i = 0; i < 2000 && c.IsAnimating; i++) c.Update(Dt);
        Assert.IsFalse(c.IsAnimating, "idle once momentum rests");
    }

    [TestMethod]
    public void MomentumLifecycleEventsFire()
    {
        var c = Make(new ScrollBehaviour { Overscroll = OverscrollMode.Clamp });
        var begun = 0;
        var ended = 0;
        c.MomentumBegin += _ => begun++;
        c.MomentumEnd += _ => ended++;

        c.BeginDrag();
        for (var i = 0; i < 3; i++) c.Drag(new Vector2(0, -25), Dt);
        c.EndDrag();
        for (var i = 0; i < 2000 && c.IsAnimating; i++) c.Update(Dt);

        Assert.AreEqual(1, begun, "momentum begin should fire once");
        Assert.AreEqual(1, ended, "momentum end should fire once");
    }
}
