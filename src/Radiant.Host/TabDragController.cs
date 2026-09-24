using System.Numerics;

namespace Radiant.Host;

/// <summary>How a tab drag resolved on release.</summary>
internal enum DragOutcomeKind
{
    /// <summary>No drag happened (a click, or no active drag) — nothing to do.</summary>
    None,

    /// <summary>Drop inside this strip: reorder the tab to <see cref="DragOutcome.InsertionIndex"/>.</summary>
    Reorder,

    /// <summary>Drop below the strip over no other window: tear the tab out into a new host window.</summary>
    TearOff,

    /// <summary>Drop over another host window: hand the tab over to that host (merge/re-attach).</summary>
    Merge,

    /// <summary>The drag was cancelled (e.g. Escape) — leave the tab where it was.</summary>
    Cancel,
}

/// <summary>The resolved result of a tab drag.</summary>
/// <param name="Kind">Which outcome fired.</param>
/// <param name="SourceIndex">The strip index the drag started on (-1 when <see cref="DragOutcomeKind.None"/>).</param>
/// <param name="InsertionIndex">For <see cref="DragOutcomeKind.Reorder"/>, the destination gap; else -1.</param>
internal readonly record struct DragOutcome(DragOutcomeKind Kind, int SourceIndex, int InsertionIndex)
{
    public static readonly DragOutcome None = new(DragOutcomeKind.None, -1, -1);

    public static DragOutcome Cancel(int src) => new(DragOutcomeKind.Cancel, src, -1);

    public static DragOutcome TearOff(int src) => new(DragOutcomeKind.TearOff, src, -1);

    public static DragOutcome Merge(int src) => new(DragOutcomeKind.Merge, src, -1);

    public static DragOutcome Reorder(int src, int to) => new(DragOutcomeKind.Reorder, src, to);
}

/// <summary>The live, per-frame state of an in-flight drag (drives placeholder/overlay rendering and the
/// live tear-off trigger), as opposed to <see cref="DragOutcome"/> which is resolved once on release.</summary>
internal enum LiveDragKind
{
    /// <summary>No drag in flight (or a sole-tab window-drag that owns its own feedback).</summary>
    Idle,

    /// <summary>Cursor within this host's strip band: show a drop placeholder at the insertion gap.</summary>
    ReorderInStrip,

    /// <summary>Cursor over another host's strip: that host shows the placeholder; on release we merge.</summary>
    MergeOverStrip,

    /// <summary>Cursor below the strip over no other strip: the floating thumbnail follows; past the
    /// tear-off threshold this is the live tear-off trigger.</summary>
    FloatingBelow,

    /// <summary>The tab has already been torn off into a following window (terminal until release).</summary>
    TornOffFollowing,
}

/// <summary>A per-frame live drag classification.</summary>
/// <param name="Kind">Which live state the drag is in this frame.</param>
/// <param name="InsertionGap">For <see cref="LiveDragKind.ReorderInStrip"/>, the gap the tab would land at; else -1.</param>
internal readonly record struct LiveDragState(LiveDragKind Kind, int InsertionGap)
{
    public static readonly LiveDragState Idle = new(LiveDragKind.Idle, -1);
    public static readonly LiveDragState Floating = new(LiveDragKind.FloatingBelow, -1);
    public static readonly LiveDragState Merge = new(LiveDragKind.MergeOverStrip, -1);
    public static readonly LiveDragState TornOff = new(LiveDragKind.TornOffFollowing, -1);
}

/// <summary>
/// The pure state machine behind the tab-strip drag gesture, lifted out of <c>LiveHost</c> so the
/// threshold/classification logic is testable without a live window. A press on a tab cell
/// (<see cref="Begin"/>) starts tracking; movement past <c>dragStartThreshold</c> promotes it to a
/// drag (<see cref="Move"/>); <see cref="Release"/> classifies the drop into a <see cref="DragOutcome"/>.
///
/// <para>The classifier is pure: the live "is the cursor over another host window?" decision is resolved
/// by the caller (from cross-process window rects) and passed in as <c>hasMergeTarget</c>, so this type
/// has no dependency on the registry, the GPU, or the global cursor.</para>
/// </summary>
internal sealed class TabDragController
{
    private readonly float _stripHeight;
    private readonly float _dragStartThreshold;
    private readonly float _tearOffThreshold;
    private Vector2 _start;

    public TabDragController(float stripHeight, float dragStartThreshold, float tearOffThreshold)
    {
        _stripHeight = stripHeight;
        _dragStartThreshold = dragStartThreshold;
        _tearOffThreshold = tearOffThreshold;
    }

    /// <summary>The strip index this drag started on, or -1 when idle.</summary>
    public int SourceIndex { get; private set; } = -1;

    /// <summary>Whether the press has moved far enough to count as a drag (vs a click).</summary>
    public bool Dragging { get; private set; }

    /// <summary>The latest pointer position (window-local), valid while a drag is active.</summary>
    public Vector2 Position { get; private set; }

    /// <summary>The dragged tab's label (for the drag-ghost).</summary>
    public string Label { get; private set; } = "";

    /// <summary>Whether this drag has already torn its tab off into a following window (set mid-drag).</summary>
    public bool IsTornOff { get; private set; }

    /// <summary>Latch the drag into the torn-off state (the live tear-off has fired); cleared on reset.</summary>
    public void MarkTornOff() => IsTornOff = true;

    /// <summary>Begin tracking a press on the tab cell at <paramref name="tabIndex"/>.</summary>
    public void Begin(int tabIndex, Vector2 pointer, string label)
    {
        SourceIndex = tabIndex;
        _start = pointer;
        Position = pointer;
        Label = label;
        Dragging = false;
    }

    /// <summary>Advance the held drag to <paramref name="pointer"/>, promoting press → drag past the threshold.</summary>
    public void Move(Vector2 pointer)
    {
        if (SourceIndex < 0)
        {
            return;
        }
        Position = pointer;
        if (!Dragging && Vector2.Distance(pointer, _start) > _dragStartThreshold)
        {
            Dragging = true;
        }
    }

    /// <summary>
    /// Classify the drop at <paramref name="pointer"/> (window-local) and reset to idle. Precedence:
    /// cancelled → <see cref="DragOutcomeKind.Cancel"/>; over another host (<paramref name="hasMergeTarget"/>)
    /// → <see cref="DragOutcomeKind.Merge"/>; <paramref name="soleTab"/> → <see cref="DragOutcomeKind.None"/>
    /// (a host's only tab never reorders or tears off — the whole window moved with the cursor instead, so
    /// dropping it anywhere but over another host is a no-op that leaves the window where it landed; this is
    /// what keeps a host from ever being emptied by a tear-off); dragged more than the tear-off threshold
    /// below the strip → <see cref="DragOutcomeKind.TearOff"/>; otherwise <see cref="DragOutcomeKind.Reorder"/>
    /// to the gap under the cursor (<see cref="TabStripLayout.InsertionIndexAt"/>). A press that never became
    /// a drag is <see cref="DragOutcomeKind.None"/>.
    /// </summary>
    public DragOutcome Release(Vector2 pointer, bool hasMergeTarget, bool cancelled, TabStripLayout layout, bool soleTab)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var src = SourceIndex;
        var dragging = Dragging;
        Reset();

        if (src < 0 || !dragging)
        {
            return DragOutcome.None;
        }
        if (cancelled)
        {
            return DragOutcome.Cancel(src);
        }
        if (hasMergeTarget)
        {
            return DragOutcome.Merge(src);
        }
        if (soleTab)
        {
            return DragOutcome.None;
        }
        if (pointer.Y > _stripHeight + _tearOffThreshold)
        {
            return DragOutcome.TearOff(src);
        }
        return DragOutcome.Reorder(src, layout.InsertionIndexAt(pointer.X));
    }

    /// <summary>
    /// Classify the in-flight drag this frame (vs <see cref="Release"/>, which resolves the drop once).
    /// Precedence mirrors <see cref="Release"/>: not dragging → <see cref="LiveDragKind.Idle"/>; already
    /// torn off → <see cref="LiveDragKind.TornOffFollowing"/>; over another host's strip
    /// (<paramref name="overOtherStrip"/>) → <see cref="LiveDragKind.MergeOverStrip"/>;
    /// <paramref name="soleTab"/> → <see cref="LiveDragKind.Idle"/> (the whole-window drag owns feedback);
    /// past the tear-off threshold below the strip → <see cref="LiveDragKind.FloatingBelow"/>; otherwise
    /// <see cref="LiveDragKind.ReorderInStrip"/> at the gap under the cursor.
    /// </summary>
    public LiveDragState Classify(Vector2 pointer, bool overOtherStrip, bool soleTab, TabStripLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (SourceIndex < 0 || !Dragging)
        {
            return LiveDragState.Idle;
        }
        if (IsTornOff)
        {
            return LiveDragState.TornOff;
        }
        if (overOtherStrip)
        {
            return LiveDragState.Merge;
        }
        if (soleTab)
        {
            return LiveDragState.Idle;
        }
        if (pointer.Y > _stripHeight + _tearOffThreshold)
        {
            return LiveDragState.Floating;
        }
        return new LiveDragState(LiveDragKind.ReorderInStrip, layout.InsertionIndexAt(pointer.X));
    }

    /// <summary>Abandon any in-progress drag (idempotent).</summary>
    public void Reset()
    {
        SourceIndex = -1;
        Dragging = false;
        Label = "";
        IsTornOff = false;
    }
}
