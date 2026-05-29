using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Gestures;
using Radiant.Input;
using Radiant.Scrolling;

namespace Radiant.Tests.Gestures;

[TestClass]
public class GestureArbiterTests
{
    private const double Dt = 1.0 / 60.0;

    private static PointerFrame Press(Vector2 p) => new(p, Vector2.Zero, Vector2.Zero, true, true, false, Dt);
    private static PointerFrame Drag(Vector2 p, Vector2 d) => new(p, d, Vector2.Zero, true, false, false, Dt);
    private static PointerFrame Release(Vector2 p) => new(p, Vector2.Zero, Vector2.Zero, false, false, true, Dt);

    [TestMethod]
    public void PanProgressesThroughStateMachine()
    {
        var pan = new PanGesture { Axis = ScrollAxes.Both, ActivationThreshold = 3f };
        var begin = 0; var change = 0; var end = 0;
        pan.OnBegin = _ => begin++;
        pan.OnChange = _ => change++;
        pan.OnEnd = _ => end++;
        var det = new GestureDetector(pan);

        det.Update(Press(new Vector2(10, 10)), true);
        Assert.AreEqual(GestureState.Possible, pan.State);

        det.Update(Drag(new Vector2(15, 10), new Vector2(5, 0)), true);
        Assert.AreEqual(GestureState.Active, pan.State);
        Assert.AreEqual(1, begin);

        det.Update(Drag(new Vector2(20, 10), new Vector2(5, 0)), true);
        Assert.AreEqual(GestureState.Active, pan.State);

        det.Update(Release(new Vector2(20, 10)), true);
        Assert.AreEqual(GestureState.Ended, pan.State);
        Assert.AreEqual(1, end);
        Assert.AreEqual(2, change, "one OnChange per active frame (activation + one drag)");
    }

    [TestMethod]
    public void PanBelowThresholdNeverActivates()
    {
        var pan = new PanGesture { Axis = ScrollAxes.Both, ActivationThreshold = 10f };
        var begin = 0;
        pan.OnBegin = _ => begin++;
        var det = new GestureDetector(pan);

        det.Update(Press(new Vector2(10, 10)), true);
        det.Update(Drag(new Vector2(12, 10), new Vector2(2, 0)), true);
        det.Update(Release(new Vector2(12, 10)), true);

        Assert.AreEqual(0, begin, "tiny travel must not activate a pan");
    }

    [TestMethod]
    public void TapRequiresPanToFail_DragSuppressesTap()
    {
        var pan = new PanGesture { Axis = ScrollAxes.Both, ActivationThreshold = 3f };
        var tap = new TapGesture { MaxTravel = 50f };
        GestureComposition.Exclusive(pan, tap); // tap requires pan to fail
        var tapFired = 0; var panBegan = 0;
        tap.OnEnd = _ => tapFired++;
        pan.OnBegin = _ => panBegan++;
        var det = new GestureDetector(pan, tap);

        det.Update(Press(new Vector2(10, 10)), true);
        det.Update(Drag(new Vector2(20, 10), new Vector2(10, 0)), true); // pan activates
        det.Update(Release(new Vector2(20, 10)), true);

        Assert.AreEqual(1, panBegan);
        Assert.AreEqual(0, tapFired, "a drag must not also fire a tap");
    }

    [TestMethod]
    public void TapRequiresPanToFail_ClickFiresTap()
    {
        var pan = new PanGesture { Axis = ScrollAxes.Both, ActivationThreshold = 3f };
        var tap = new TapGesture { MaxTravel = 5f };
        GestureComposition.Exclusive(pan, tap);
        var tapFired = 0;
        tap.OnEnd = _ => tapFired++;
        var det = new GestureDetector(pan, tap);

        det.Update(Press(new Vector2(10, 10)), true);
        det.Update(Release(new Vector2(10, 10)), true); // press+release, no travel

        Assert.AreEqual(1, tapFired, "a clean click should fire the tap");
    }

    [TestMethod]
    public void RaceGivesPriorityToFirstAndStickyOwnershipHolds()
    {
        var first = new PanGesture { Axis = ScrollAxes.Both, ActivationThreshold = 3f };
        var second = new PanGesture { Axis = ScrollAxes.Both, ActivationThreshold = 3f };
        var firstBegan = 0; var secondBegan = 0;
        first.OnBegin = _ => firstBegan++;
        second.OnBegin = _ => secondBegan++;
        var det = new GestureDetector(GestureComposition.Race(first, second));

        det.Update(Press(new Vector2(10, 10)), true);
        det.Update(Drag(new Vector2(20, 10), new Vector2(10, 0)), true); // both want; first wins
        det.Update(Drag(new Vector2(30, 10), new Vector2(10, 0)), true); // still first; not stolen
        det.Update(Release(new Vector2(30, 10)), true);

        Assert.AreEqual(1, firstBegan);
        Assert.AreEqual(0, secondBegan, "loser never activates; owner is sticky");
        Assert.AreSame(null, det.Owner);
    }

    [TestMethod]
    public void DetectorReportsCaptureWhilePressedOrActive()
    {
        var pan = new PanGesture { Axis = ScrollAxes.Both, ActivationThreshold = 3f };
        var det = new GestureDetector(pan);

        det.Update(Press(new Vector2(10, 10)), true);
        Assert.IsTrue(det.HasActiveOrClaimingOwner, "a press inside should claim capture");

        det.Update(Drag(new Vector2(20, 10), new Vector2(10, 0)), true);
        Assert.IsTrue(det.HasActiveOrClaimingOwner, "active drag captures");

        det.Update(Release(new Vector2(20, 10)), true);
        det.Update(new PointerFrame(new Vector2(20, 10), Vector2.Zero, Vector2.Zero, false, false, false, Dt), true);
        Assert.IsFalse(det.HasActiveOrClaimingOwner, "idle after release");
    }
}
