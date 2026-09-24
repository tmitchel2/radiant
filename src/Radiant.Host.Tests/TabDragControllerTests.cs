using System.Numerics;

namespace Radiant.Host.Tests;

[TestClass]
public sealed class TabDragControllerTests
{
    private const float StripHeight = 28f;
    private const float DragStart = 6f;
    private const float TearOff = 24f;

    private static TabDragController NewController() => new(StripHeight, DragStart, TearOff);

    private static TabStripLayout Layout() => new(400, 3); // width 133.33

    [TestMethod]
    public void PressWithoutMovingIsNotADrag()
    {
        var c = NewController();
        c.Begin(0, new Vector2(10, 5), "a");
        Assert.IsFalse(c.Dragging);
        var outcome = c.Release(new Vector2(11, 5), hasMergeTarget: false, cancelled: false, Layout(), soleTab: false);
        Assert.AreEqual(DragOutcomeKind.None, outcome.Kind);
    }

    [TestMethod]
    public void MovePastThresholdBecomesDrag()
    {
        var c = NewController();
        c.Begin(1, new Vector2(0, 5), "b");
        c.Move(new Vector2(10, 5)); // 10 > 6
        Assert.IsTrue(c.Dragging);
        Assert.AreEqual(1, c.SourceIndex);
    }

    [TestMethod]
    public void DropInsideStripReorders()
    {
        var c = NewController();
        c.Begin(0, new Vector2(10, 5), "a");
        c.Move(new Vector2(250, 5));
        var outcome = c.Release(new Vector2(250, 5), hasMergeTarget: false, cancelled: false, Layout(), soleTab: false);
        Assert.AreEqual(DragOutcomeKind.Reorder, outcome.Kind);
        Assert.AreEqual(0, outcome.SourceIndex);
        Assert.AreEqual(Layout().InsertionIndexAt(250), outcome.InsertionIndex);
    }

    [TestMethod]
    public void DropBelowStripTearsOff()
    {
        var c = NewController();
        c.Begin(0, new Vector2(10, 5), "a");
        c.Move(new Vector2(10, 100)); // below strip + threshold
        var outcome = c.Release(new Vector2(10, 100), hasMergeTarget: false, cancelled: false, Layout(), soleTab: false);
        Assert.AreEqual(DragOutcomeKind.TearOff, outcome.Kind);
        Assert.AreEqual(0, outcome.SourceIndex);
    }

    [TestMethod]
    public void DropOverAnotherHostMergesRegardlessOfPosition()
    {
        var c = NewController();
        c.Begin(2, new Vector2(10, 5), "c");
        c.Move(new Vector2(10, 100));
        var outcome = c.Release(new Vector2(10, 100), hasMergeTarget: true, cancelled: false, Layout(), soleTab: false);
        Assert.AreEqual(DragOutcomeKind.Merge, outcome.Kind);
        Assert.AreEqual(2, outcome.SourceIndex);
    }

    [TestMethod]
    public void CancelledDragReportsCancel()
    {
        var c = NewController();
        c.Begin(0, new Vector2(10, 5), "a");
        c.Move(new Vector2(60, 5));
        var outcome = c.Release(new Vector2(60, 5), hasMergeTarget: false, cancelled: true, Layout(), soleTab: false);
        Assert.AreEqual(DragOutcomeKind.Cancel, outcome.Kind);
    }

    [TestMethod]
    public void SoleTabDroppedBelowStripDoesNotTearOff()
    {
        // A host's only tab: dragging it moved the whole window, so dropping it below the strip over
        // no other host is a no-op (the window stays where it landed) — never a tear-off that would
        // leave the source host with zero tabs.
        var c = NewController();
        c.Begin(0, new Vector2(10, 5), "a");
        c.Move(new Vector2(10, 100)); // below strip + threshold — would be a tear-off for a non-sole tab
        var outcome = c.Release(new Vector2(10, 100), hasMergeTarget: false, cancelled: false, Layout(), soleTab: true);
        Assert.AreEqual(DragOutcomeKind.None, outcome.Kind);
    }

    [TestMethod]
    public void SoleTabDroppedInStripDoesNotReorder()
    {
        var c = NewController();
        c.Begin(0, new Vector2(10, 5), "a");
        c.Move(new Vector2(250, 5));
        var outcome = c.Release(new Vector2(250, 5), hasMergeTarget: false, cancelled: false, Layout(), soleTab: true);
        Assert.AreEqual(DragOutcomeKind.None, outcome.Kind);
    }

    [TestMethod]
    public void SoleTabDroppedOverAnotherHostStillMerges()
    {
        // Dragging the only tab over another window still merges it in (the source host then empties
        // and closes) — that is the one way a sole-tab drag legitimately hands the tab away.
        var c = NewController();
        c.Begin(0, new Vector2(10, 5), "a");
        c.Move(new Vector2(10, 100));
        var outcome = c.Release(new Vector2(10, 100), hasMergeTarget: true, cancelled: false, Layout(), soleTab: true);
        Assert.AreEqual(DragOutcomeKind.Merge, outcome.Kind);
        Assert.AreEqual(0, outcome.SourceIndex);
    }

    [TestMethod]
    public void ClassifyIdleBeforeDragStarts()
    {
        var c = NewController();
        c.Begin(0, new Vector2(10, 5), "a"); // press, not yet moved past threshold
        var state = c.Classify(new Vector2(11, 5), overOtherStrip: false, soleTab: false, Layout());
        Assert.AreEqual(LiveDragKind.Idle, state.Kind);
    }

    [TestMethod]
    public void ClassifyReorderInStripWithinBand()
    {
        var c = NewController();
        c.Begin(0, new Vector2(10, 5), "a");
        c.Move(new Vector2(250, 5)); // dragging, inside strip band
        var state = c.Classify(new Vector2(250, 5), overOtherStrip: false, soleTab: false, Layout());
        Assert.AreEqual(LiveDragKind.ReorderInStrip, state.Kind);
        Assert.AreEqual(Layout().InsertionIndexAt(250), state.InsertionGap);
    }

    [TestMethod]
    public void ClassifyFloatingBelowPastTearOff()
    {
        var c = NewController();
        c.Begin(0, new Vector2(10, 5), "a");
        c.Move(new Vector2(10, 100)); // below strip + tear-off threshold
        var state = c.Classify(new Vector2(10, 100), overOtherStrip: false, soleTab: false, Layout());
        Assert.AreEqual(LiveDragKind.FloatingBelow, state.Kind);
    }

    [TestMethod]
    public void ClassifyMergeOverOtherStripTakesPrecedence()
    {
        var c = NewController();
        c.Begin(0, new Vector2(10, 5), "a");
        c.Move(new Vector2(10, 100)); // below own strip, but cursor is over another host's strip
        var state = c.Classify(new Vector2(10, 100), overOtherStrip: true, soleTab: false, Layout());
        Assert.AreEqual(LiveDragKind.MergeOverStrip, state.Kind);
    }

    [TestMethod]
    public void ClassifySoleTabIsIdle()
    {
        var c = NewController();
        c.Begin(0, new Vector2(10, 5), "a");
        c.Move(new Vector2(10, 100));
        var state = c.Classify(new Vector2(10, 100), overOtherStrip: false, soleTab: true, Layout());
        Assert.AreEqual(LiveDragKind.Idle, state.Kind);
    }

    [TestMethod]
    public void ClassifyTornOffIsTerminalUntilReset()
    {
        var c = NewController();
        c.Begin(0, new Vector2(10, 5), "a");
        c.Move(new Vector2(10, 100));
        c.MarkTornOff();
        Assert.IsTrue(c.IsTornOff);
        // Even back inside the strip band, a torn-off drag stays following until release/reset.
        var state = c.Classify(new Vector2(200, 5), overOtherStrip: false, soleTab: false, Layout());
        Assert.AreEqual(LiveDragKind.TornOffFollowing, state.Kind);
        c.Reset();
        Assert.IsFalse(c.IsTornOff);
    }

    [TestMethod]
    public void ResetClearsActiveDrag()
    {
        var c = NewController();
        c.Begin(0, new Vector2(10, 5), "a");
        c.Move(new Vector2(60, 5));
        c.Reset();
        Assert.IsFalse(c.Dragging);
        Assert.AreEqual(-1, c.SourceIndex);
        var outcome = c.Release(new Vector2(60, 5), hasMergeTarget: false, cancelled: false, Layout(), soleTab: false);
        Assert.AreEqual(DragOutcomeKind.None, outcome.Kind);
    }
}
