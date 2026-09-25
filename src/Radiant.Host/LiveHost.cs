using System.Diagnostics;
using System.Numerics;
using Radiant.Graphics;
using Radiant.Graphics2D;
using Radiant.Host.AgentControlProtocol;
using Radiant.Host.Ipc;
using Radiant.Host.Ipc.Frames;
using Radiant.Host.Ipc.Input;
using Radiant.Input;
using Silk.NET.Input;
using Silk.NET.WebGPU;

namespace Radiant.Host;

/// <summary>
/// The live windowed compositing host: opens one OS window via <see cref="RadiantApplication"/>,
/// discovers running <c>--attach</c> renderers (instances that publish a frame buffer), and each
/// frame blits the active tab's latest frame under a tab strip via <see cref="HostCompositor"/>.
///
/// <para>Tab switching: number keys 1–9, or click a tab cell. The tab set refreshes from the
/// instance registry once a second. Per-frame pixels arrive over shared memory; window input is
/// forwarded to the active tab's renderer through an <see cref="InputRing"/> (mouse coordinates
/// offset so the renderer sees its content area as origin, with the strip excluded). This is the
/// "browser process" of the Chrome-style model.</para>
/// </summary>
internal sealed unsafe class LiveHost : IDisposable
{
    private const int MaxFrameBytes = 3840 * 2160 * 4;

    private static readonly Key[] s_numberKeys =
    [
        Key.Number1, Key.Number2, Key.Number3, Key.Number4, Key.Number5,
        Key.Number6, Key.Number7, Key.Number8, Key.Number9,
    ];

    // Mouse buttons we forward (Silk.NET MouseButton int values are stable on the InputRing wire).
    private static readonly MouseButton[] s_forwardedButtons = [MouseButton.Left, MouseButton.Right, MouseButton.Middle];

    // EVERY key the toolkit defines, less the ones this host consumes for itself. Derived rather
    // than listed: a host serves tabs it knows nothing about, so an explicit list would mean an
    // upstream commit every time some tab wanted a key -- which is how a renderer ends up with no
    // keyboard at all. The number keys are excluded because Update() takes them unconditionally for
    // tab activation, and forwarding a key the host has already acted on is how a tab and its host
    // both respond to one press.
    //
    // Escape IS forwarded: the host consumes it only inside an active tab drag, where the pointer is
    // in the strip and the tab is receiving nothing anyway.
    //
    // THE Distinct() IS LOAD-BEARING AND WAS MEASURED. Silk's Key enum has 122 members and 121
    // distinct values -- Number0 and D0 are both 48 -- and zero is not one of the digits Update()
    // takes, so it survives the filter under both names. Without the collapse, one press of 0 would
    // arrive at the tab as two.
    private static readonly Key[] s_forwardedKeys =
    [
        .. Enum.GetValues<Key>()
            .Where(key => key != Key.Unknown && !s_numberKeys.Contains(key))
            .Distinct()
            .Order(),
    ];

    /// <summary>Which keys reach a renderer tab. Derived; see <c>s_forwardedKeys</c>.</summary>
    internal static IReadOnlyList<Key> ForwardedKeys => s_forwardedKeys;

    private readonly TabController _controller;
    private readonly string _hostName;
    private readonly byte[] _frameScratch = new byte[MaxFrameBytes];

    private CommandReceiver? _receiver;
    private SharedFrameBuffer? _activeReader;
    private string? _activeReaderName;
    // One input ring per owned tab, kept for the tab's whole lifetime (created lazily, disposed only
    // when the tab closes). The active tab carries forwarded input + Active=1; every background tab
    // carries Active=0 so its renderer pauses rendering. A stable ring per tab (vs. create/delete on
    // every switch) keeps the renderer's reader valid across activations.
    private readonly Dictionary<string, InputRing> _inputRings = [];
    private Texture2D? _tabTexture;
    private string? _renderedTabName;
    private MsdfFont? _font;
    private double _rescanAccumulator;
    private long _frameCount;
    private int _lastTabWidth = 1280;
    private int _lastTabHeight = 720;
    private bool _hasHadTabs;
    private int _hoveredTab = -1;

    // The source-side live drag classification this frame (drives our own placeholder/ghost in Render).
    private LiveDragState _liveState = LiveDragState.Idle;
    // The insertion gap of an incoming drag from ANOTHER host whose cursor is over our strip (-1 = none),
    // so we draw the "potential tab" placeholder for a tab about to merge into us. Read from drag.json.
    private int _incomingGap = -1;
    private string _incomingLabel = "";

    // Tab-strip drag gesture. A press on a tab cell starts a drag; on release it resolves to a reorder
    // (dropped in this strip), a tear-off (dropped below the strip over no other window), or a merge
    // (dropped over another host window). The threshold/classification logic lives in the pure
    // TabDragController; LiveHost feeds it live input and resolves the merge target from window rects.
    private const float DragStartThreshold = 6f;   // px of movement before a press becomes a drag
    private const float TearOffThreshold = 24f;     // px below the strip before a drag tears the tab out
    // How close to another host's tab strip a drag counts as "over" it for the live merge preview AND the
    // drop-merge resolution (the strip band grown by this margin, notably downward) — so a tab merges /
    // previews when merely near the tabs, Chrome-style, not only strictly inside the strip rect.
    private const float StripProximityMargin = 24f;
    private readonly TabDragController _drag = new(HostCompositor.StripHeight, DragStartThreshold, TearOffThreshold);
    private WindowBounds? _publishedBounds;

    // Single-tab window-drag (Chrome's "drag the only tab → move the whole window"). Armed when a press
    // begins while this host owns exactly one tab: once the press promotes to a drag, the host follows the
    // cursor by repositioning its OS window each frame rather than tearing the lone tab into a new process
    // (which would momentarily empty the host — a host must never be left with zero tabs). _windowDragGrab
    // is the window-local mouse position at press, kept under the cursor as the window moves. On release a
    // drop over another host still merges (then this host empties and closes); anywhere else is a no-op.
    private bool _windowDragArmed;
    private Vector2 _windowDragGrab;
    // While a sole-tab window-drag hovers another host's strip, this window hides (Chrome's "drag the last
    // tab into another window" — the tab previews in the target, this window disappears) and reappears if
    // the cursor moves off the strip. Tracked so SetVisible is only toggled on transitions.
    private bool _windowDragHidden;

    /// <summary>True while a sole-tab drag is actively moving this window (vs. a normal in-strip drag).</summary>
    private bool WindowDragging => _windowDragArmed && _drag.Dragging;

    // The host-owned buffer the source host publishes the dragged tab's downscaled frame into, so the
    // floating overlay can show a live thumbnail without opening the renderer's single-consumer frame
    // buffer. Producer = this host; consumer = the overlay process. Created lazily on drag, dropped after.
    private SharedFrameBuffer? _thumbWriter;
    private byte[]? _thumbScratch;
    private int _frameW;
    private int _frameH;

    // The overlay thumbnail's maximum box (the source frame's aspect is preserved within it). Matches the
    // overlay window's content size in DragOverlay.
    private const int ThumbMaxWidth = 192;
    private const int ThumbMaxHeight = 120;

    // Live cross-window merge preview. The content-owning host (the source for a direct merge, or the
    // hidden follower after a tear-off) republishes the dragged tab's already-read frame into a host-owned
    // full-res buffer (a clean second single-producer/single-consumer channel, never the renderer's
    // single-consumer frame buffer) so the TARGET host can composite the dragged tab's live content while
    // it hovers. Producer side (this host owns the frame):
    private SharedFrameBuffer? _previewWriter;
    private int _previewWriterW;
    private int _previewWriterH;
    // Set each Update frame: true when this host should republish its active frame as a merge preview —
    // i.e. the source dragging over another host's strip, or the hidden follower over a re-merge strip.
    private bool _publishMergePreview;
    // The strip target the drag driver resolved this frame (authoritative single consumer of the preview),
    // broadcast in drag.json so exactly one host shows the preview even when windows overlap. "" = none.
    private string _dragTargetHost = "";

    // Consumer side (this host is the TARGET showing an incoming tab's live content):
    private SharedFrameBuffer? _previewReader;
    private string? _previewReaderPath;
    private Texture2D? _previewTexture;
    private byte[]? _previewScratch;
    private string _incomingPreviewPath = "";

    /// <summary>
    /// Per-frame drag-input tracing (<c>&lt;prefix&gt;_DRAG_DEBUG=1</c>): logs the live pointer + button state
    /// while a strip drag is in flight, to diagnose "tab flickers but won't follow the cursor" — i.e.
    /// whether the host stops receiving mouse-move or loses the held button mid-drag. See TABS_ROADMAP.md.
    /// </summary>
    private readonly bool _dragDebug =
        RadiantAppIdentity.Current.EnvironmentIs("DRAG_DEBUG", "1");

    // Live tear-off (source side): once a multi-tab drag floats past the tear-off threshold the tab is
    // detached NOW into a following host window, and this host (still holding the mouse button) keeps
    // driving the cursor broadcast in drag.json until release, when it resolves the re-merge target.
    private bool _tornOffActive;
    private string _tornOffHost = "";
    private string _tornOffTab = "";
    private const float TearOffGrabOffsetX = 80f; // cursor sits this far into the torn-off window (over its strip)

    // Live tear-off (follower side, `--follow`): this host owns one torn-off tab and follows the cursor by
    // reading drag.json (NOT its own GlobalCursor — its window is being moved every frame). It starts hidden
    // so it can position itself under the cursor before showing (no spawn-origin flash, no focus-steal).
    private bool _startHidden;
    private bool _followShown;
    private double _followIdle;
    private const double FollowSettleSeconds = 2.0; // give up waiting for a follow session after this

    public LiveHost(string hostName, TabController? controller = null, bool startHidden = false)
    {
        _hostName = hostName;
        // The default controller wires window.focus back to this host: the macOS Dock menu sends
        // window.focus cross-process, and the handler sets a flag consumed on the run-loop thread in Update
        // (all RadiantApplication calls must stay on that thread).
        _controller = controller ?? new TabController(hostName, focusWindow: () => _focusRequested = true);
        _startHidden = startHidden;
    }

    // Set by the window.focus command handler (CommandReceiver thread), consumed in Update on the run-loop
    // thread so the actual RadiantApplication.Focus() / app activation happens where the window is owned.
    private volatile bool _focusRequested;

    // The active tab name last published to active-tab.txt (for the Dock owner's per-host menu); republished
    // only on change.
    private string? _publishedActiveTab;

    // macOS Dock consolidation: exactly one host owns the single Dock tile (Regular policy + icon + menu);
    // the others are accessory apps (no tile). Decided once on the first frame, then maintained (menu
    // install + re-election if the owner dies) each Update.
    private bool _isDockOwner;
    private bool _dockPresenceApplied;
    private double _dockReelectAccumulator;

    public void Run()
    {
        RegisterHost();
        _receiver = new CommandReceiver(_hostName);
        _controller.Refresh();
        EnsureDragOverlay();
        Console.WriteLine($"Radiant.Host '{_hostName}' starting — {_controller.Tabs.Count} tab(s): {string.Join(", ", _controller.Tabs)}");
        try
        {
            using var app = new RadiantApplication();
            // A live tear-off follower starts hidden + focus-off + topmost + mouse-passthrough (it reveals
            // itself once positioned under the cursor, floating in front of its source); a normal host opens
            // visible. FocusOnShow=false avoids the spurious mouse-up that a focus-stealing show would deliver
            // to the source host and end the drag; MousePassthrough=true makes the follower click-through while
            // it tracks the cursor, so showing/hiding it directly under the cursor mid-drag can't churn the
            // source window's pointer state and synthesize a mouse-up (same class of bug as the drag overlay —
            // see TABS.md). TopMost (which does NOT steal focus) keeps it in front during the drag — on settle
            // it drops topmost + passthrough and focuses (see TABS.md, UpdateFollow).
            var style = _startHidden
                ? new RadiantWindowStyle { Visible = false, FocusOnShow = false, TopMost = true, MousePassthrough = true }
                : null;
            // A follower opens at the source host's current size (the torn tab was rendering at that size);
            // a normal host uses the default. Resolved from the in-flight drag session's source rect.
            var (w, h) = FollowerWindowSize.Resolve(
                _startHidden ? DragSession.Read() : null,
                HostWindowBounds.Read,
                (1280, 720 + (int)HostCompositor.StripHeight));
            if (style is not null)
            {
                app.Run(
                    title: RadiantAppIdentity.Current.Name,
                    width: w,
                    height: h,
                    handedness: Handedness.RightHanded,
                    renderCallback: r => Render(app, r),
                    updateCallback: dt => Update(app, dt),
                    backgroundColor: new Vector4(0.05f, 0.05f, 0.06f, 1f),
                    style: style);
            }
            else
            {
                app.Run(
                    title: RadiantAppIdentity.Current.Name,
                    width: w,
                    height: h,
                    handedness: Handedness.RightHanded,
                    renderCallback: r => Render(app, r),
                    updateCallback: dt => Update(app, dt),
                    backgroundColor: new Vector4(0.05f, 0.05f, 0.06f, 1f));
            }
        }
        finally
        {
            InstanceRegistry.Deregister(_hostName);
        }
    }

    private void RegisterHost()
    {
        InstanceRegistry.Register(new InstanceInfo
        {
            Name = _hostName,
            Pid = Environment.ProcessId,
            StartTime = DateTime.UtcNow.ToString("o"),
            WorkingDirectory = Directory.GetCurrentDirectory(),
            Capabilities = ["tab"],
            ProtocolVersion = TabProtocol.Version,
        });
    }

    private void Update(RadiantApplication app, double dt)
    {
        // Install the native macOS File menu on every host window (idempotent; no-op off macOS and once
        // installed). Done here rather than in the Dock-presence path so secondary windows get it too.
        MacMainMenu.TryInstall(app, _controller);

        // Recomputed each frame by UpdateFollow (follower side) / UpdateLiveDrag (source side); default off
        // so a host that is neither following nor dragging over a strip stops publishing a stale preview.
        _publishMergePreview = false;

        // Periodically rescan for attached renderers (new tabs appearing / closing).
        _rescanAccumulator += dt;
        if (_rescanAccumulator > 1.0)
        {
            _rescanAccumulator = 0;
            _controller.Refresh();
        }

        // Apply any agent-control commands (tab.list/activate/spawn/close/adopt/window.focus) on the host
        // thread, so mutating the tab set here is race-free with rendering and input forwarding.
        DrainCommands();

        // A window.focus command (e.g. from the macOS Dock menu) raised this flag on the receiver thread —
        // do the actual focus here, on the run-loop thread that owns the window.
        if (_focusRequested)
        {
            _focusRequested = false;
            app.Focus();
            MacWindowFocus.TryActivateApp();
        }

        // Publish this window's rect so other hosts can hit-test a drag-merge drop against it.
        PublishWindowBounds(app);

        // Publish our active tab so the Dock owner can label this host's entry in the Dock menu.
        PublishActiveTab();

        // Maintain the single Dock tile: claim/accessory decision on first frame, then keep the menu
        // installed and re-elect a new owner if the current one dies (macOS only).
        UpdateDockPresence(dt);

        // If this host is a live tear-off follower (`--follow`), track the cursor from drag.json.
        UpdateFollow(app, dt);

        var input = app.Input;
        var tabCount = _controller.Tabs.Count;

        // Number-key tab selection (1–9).
        for (var i = 0; i < s_numberKeys.Length && i < tabCount; i++)
        {
            if (input.IsKeyPressed(s_numberKeys[i]))
            {
                _controller.Activate(i);
            }
        }

        // A press inside the strip is a tab action and is NOT forwarded to the renderer.
        var pointer = input.MousePosition;
        var pointerInStrip = pointer.Y < HostCompositor.StripHeight;

        // Track the hovered tab (for the close-button reveal in Render).
        var stripLayout = new TabStripLayout(app.WindowWidth, tabCount);
        _hoveredTab = pointerInStrip && tabCount > 0 ? stripLayout.HitTest(pointer.X) : -1;

        // A press on the new-tab (+) button spawns a tab attached to this host (works at any tab count).
        if (input.IsMouseButtonPressed(MouseButton.Left) && pointerInStrip
            && pointer.X >= stripLayout.PlusRect.X && pointer.X <= stripLayout.PlusRect.X + stripLayout.PlusRect.Width)
        {
            _controller.SpawnLocalTab();
        }
        else if (input.IsMouseButtonPressed(MouseButton.Left) && pointerInStrip && tabCount > 0)
        {
            var idx = stripLayout.HitTest(pointer.X);
            // A press on a tab's close (×) button closes that tab and consumes the press (no activate/drag).
            if (idx >= 0 && stripLayout.CloseRectAt(idx) is { } close
                && pointer.X >= close.X && pointer.X <= close.X + close.Width)
            {
                _controller.CloseTab(idx);
            }
            else if (idx >= 0)
            {
                _controller.Activate(idx);
                _drag.Begin(idx, pointer, _controller.Tabs[idx]);
                // Arm window-drag when this is the host's only tab: a drag will move the window, not tear off.
                _windowDragArmed = tabCount == 1;
                _windowDragGrab = pointer;
                if (_dragDebug)
                {
                    Console.WriteLine($"[drag] BEGIN idx={idx} at ({pointer.X:F1},{pointer.Y:F1}) strip<{HostCompositor.StripHeight}");
                }
            }
        }

        UpdateDrag(app, input, pointer);

        // Detect a drag from another host hovering our strip (to draw the incoming "potential tab").
        UpdateIncomingDrag();

        ForwardInputToActiveTab(app, pointerInStrip);

        // A host closes its window once its last tab is gone (Chrome-style: closing the last tab closes
        // the window) — primary and secondary alike. The _hasHadTabs guard means this only fires after
        // at least one tab has attached, so it never races a just-spawned host whose auto-attaching
        // renderer (plain-launch) hasn't published its frame buffer yet.
        if (_controller.Tabs.Count > 0)
        {
            _hasHadTabs = true;
        }
        else if (_hasHadTabs)
        {
            app.Close();
        }
    }

    /// <summary>
    /// Drive the tab-strip drag gesture from live input: advance the drag while the button is held
    /// (Escape cancels), and on release classify the drop via <see cref="TabDragController"/> into a
    /// reorder, tear-off, or merge and act on it.
    /// </summary>
    private void UpdateDrag(RadiantApplication app, InputState input, Vector2 pointer)
    {
        if (_dragDebug && _drag.SourceIndex >= 0)
        {
            var polled = app.PolledCursorPosition;
            Console.WriteLine(
                $"[drag] held={input.IsMouseButtonDown(MouseButton.Left)} event=({pointer.X:F1},{pointer.Y:F1}) " +
                $"polled=({polled.X:F1},{polled.Y:F1}) delta=({input.MouseDelta.X:F1},{input.MouseDelta.Y:F1}) dragging={_drag.Dragging}");
        }

        if (_drag.SourceIndex >= 0 && input.IsMouseButtonDown(MouseButton.Left))
        {
            if (input.IsKeyPressed(Key.Escape))
            {
                // Escape after a live tear-off leaves the torn-off window where it is (no re-merge).
                ClearTornOff();
                _drag.Reset();
                _windowDragArmed = false;
                SetWindowDragHidden(app, false); // un-hide if a sole-tab drag was hovering a target strip
                DragSession.Clear();
            }
            else
            {
                _drag.Move(pointer);
                if (WindowDragging)
                {
                    UpdateWindowDrag(app);
                }
                else
                {
                    UpdateLiveDrag(app, pointer);
                }
            }
        }

        if (!input.IsMouseButtonReleased(MouseButton.Left))
        {
            return;
        }

        // A release that ends a live-tear-off drag resolves the re-merge (or settles the following window).
        if (_tornOffActive)
        {
            HandleTornOffRelease(app);
            return;
        }

        // Only a release that ends a strip drag clears the session (not every viewport click).
        if (_drag.SourceIndex >= 0)
        {
            DragSession.Clear();
        }

        var tabCount = _controller.Tabs.Count;
        var layout = new TabStripLayout(app.WindowWidth, Math.Max(tabCount, 1));
        // A drop over another host's tab STRIP merges into it; over our own window it reorders (in the strip)
        // or tears off (below the strip). A drop over a target window's body (away from its tabs) does NOT
        // merge — strip-only, matching the per-frame placeholder preview. A sole-tab window-drag only ever
        // merges (or no-ops) — it never reorders or tears off, so the host is never emptied except by
        // handing the tab to another host.
        var soleTab = _windowDragArmed;
        var mergeTarget = _drag.Dragging ? ResolveStripMergeTarget(app) : null;
        var outcome = _drag.Release(pointer, mergeTarget is not null, cancelled: false, layout, soleTab);
        _windowDragArmed = false;

        switch (outcome.Kind)
        {
            case DragOutcomeKind.Reorder:
                if (_controller.Reorder(outcome.SourceIndex, outcome.InsertionIndex))
                {
                    Console.WriteLine($"Radiant.Host: reordered tab {outcome.SourceIndex} → gap {outcome.InsertionIndex}");
                }
                break;
            case DragOutcomeKind.Merge:
                TryMerge(app, outcome.SourceIndex, mergeTarget!);
                break;
            case DragOutcomeKind.TearOff:
                TryTearOff(outcome.SourceIndex);
                break;
            case DragOutcomeKind.None:
            case DragOutcomeKind.Cancel:
            default:
                break;
        }

        // A sole-tab drag that was hovering a target strip hid this window. Reveal it again whenever the host
        // still has a tab (a no-op drop, or a merge that failed and kept the tab); a successful merge leaves
        // the host empty so it closes next Update — keep it hidden through that.
        if (_controller.Tabs.Count > 0)
        {
            SetWindowDragHidden(app, false);
        }
    }

    /// <summary>Publish this window's screen rect to <c>window.json</c>, but only when it actually changed.</summary>
    private void PublishWindowBounds(RadiantApplication app)
    {
        var bounds = new WindowBounds
        {
            X = app.WindowX,
            Y = app.WindowY,
            Width = app.WindowWidth,
            Height = app.WindowHeight,
            StripHeight = HostCompositor.StripHeight,
        };
        if (_publishedBounds is { } b && b.X == bounds.X && b.Y == bounds.Y
            && b.Width == bounds.Width && b.Height == bounds.Height)
        {
            return;
        }
        HostWindowBounds.Write(_hostName, bounds);
        _publishedBounds = bounds;
    }

    /// <summary>
    /// Classify the in-flight strip drag this frame and publish the drag session accordingly: when the
    /// cursor is over a strip (our own → reorder, or another host's → merge) the floating overlay hides
    /// (<c>OverStrip</c>) and an in-strip placeholder is shown instead; below any strip the overlay floats
    /// the thumbnail. The classification is stored in <see cref="_liveState"/> for <see cref="Render"/>.
    /// </summary>
    private void UpdateLiveDrag(RadiantApplication app, Vector2 pointer)
    {
        // After a live tear-off this host no longer owns the tab; it just keeps broadcasting the cursor so
        // the following window tracks it, flagging OverStrip when a re-merge target strip is under the cursor.
        // The FOLLOWER owns the frame and republishes the preview (see UpdateFollow), so the source doesn't.
        if (_tornOffActive)
        {
            _liveState = LiveDragState.TornOff;
            PublishTornOffDragSession(app);
            return;
        }

        var layout = new TabStripLayout(app.WindowWidth, Math.Max(_controller.Tabs.Count, 1));
        var mergeTarget = ResolveStripMergeTarget(app);
        _liveState = _drag.Classify(pointer, mergeTarget is not null, _windowDragArmed, layout);

        // Live tear-off: the moment a multi-tab drag floats past the tear-off threshold below all strips,
        // separate NOW into a following window (vs. waiting for the drop). Never for a sole tab (its drag
        // moves the whole window) — that would empty the source host.
        if (_liveState.Kind == LiveDragKind.FloatingBelow && !_windowDragArmed && _controller.Tabs.Count >= 2)
        {
            FireLiveTearOff();
            if (_tornOffActive)
            {
                _liveState = LiveDragState.TornOff;
                PublishTornOffDragSession(app);
                return;
            }
        }

        // Over ANOTHER host's strip → a cross-window merge: name that host as the preview target and
        // republish our active (dragged) frame so it can show the tab's live content. A reorder over our own
        // strip needs no cross-window preview (target stays "").
        var overStrip = _liveState.Kind is LiveDragKind.ReorderInStrip or LiveDragKind.MergeOverStrip;
        _dragTargetHost = _liveState.Kind == LiveDragKind.MergeOverStrip ? (mergeTarget ?? "") : "";
        _publishMergePreview = _dragTargetHost.Length > 0;
        PublishDragSession(app, overStrip);
    }

    /// <summary>Publish the drag session for a torn-off drag: the re-merge target (if any) is the preview
    /// target, and its preview frame is owned/published by the follower, not this source host.</summary>
    private void PublishTornOffDragSession(RadiantApplication app)
    {
        _dragTargetHost = ResolveReMergeStrip(app) ?? "";
        PublishDragSession(app, overStrip: _dragTargetHost.Length > 0);
    }

    /// <summary>
    /// Drive a sole-tab window-drag each frame. Normally the whole window is the feedback — it follows the
    /// cursor (the grabbed strip point pinned under the polled global cursor; the polled position is reliable
    /// during a held drag and over foreign windows, unlike the event-fed <c>GlobalCursor</c>). But when it
    /// comes within proximity of ANOTHER host's strip it behaves like a merge: this window hides and
    /// republishes its tab's frame as the live preview, so the target shows our tab landing in its strip +
    /// body (Chrome's "drag the last tab into another window"). Off any target strip it reappears and resumes
    /// following; on release a drop over a strip merges (then this host empties + closes — handled in
    /// <see cref="UpdateDrag"/>), anywhere else is a no-op that leaves the window where it landed.
    /// </summary>
    private void UpdateWindowDrag(RadiantApplication app)
    {
        var target = ResolveStripMergeTarget(app);
        if (target is not null)
        {
            _dragTargetHost = target;
            _publishMergePreview = true;
            SetWindowDragHidden(app, true);
            PublishDragSession(app, overStrip: true);
            return;
        }
        SetWindowDragHidden(app, false);
        var cursor = PolledGlobalCursor(app);
        app.MoveWindow((int)(cursor.X - _windowDragGrab.X), (int)(cursor.Y - _windowDragGrab.Y));
        _dragTargetHost = "";
        // Off any target strip the window itself is the feedback — clear the session so the overlay / a
        // previously-hovered target stop showing anything.
        DragSession.Clear();
    }

    /// <summary>Show/hide this window during a sole-tab window-drag (only toggling SetVisible on a change).</summary>
    private void SetWindowDragHidden(RadiantApplication app, bool hidden)
    {
        if (_windowDragHidden == hidden)
        {
            return;
        }
        // Go transparent rather than SetVisible(false): hiding (ordering out) the window that holds the mouse
        // button mid-drag reshuffles the macOS key window and delivers this host a spurious mouse-up that
        // commits the merge while the button is still held (you then can't drag back out). Opacity keeps the
        // window on-screen + ordered-in — invisible, but it retains the OS pointer capture that drives the drag.
        app.SetOpacity(hidden ? 0f : 1f);
        _windowDragHidden = hidden;
    }

    /// <summary>
    /// Detach the dragged tab into its own following window mid-drag (the live tear-off). This host keeps the
    /// held button and from now on only drives the cursor broadcast (<see cref="_tornOffActive"/>); the new
    /// host follows it (spawned with <c>--follow</c>, hidden until positioned).
    /// </summary>
    private void FireLiveTearOff()
    {
        var index = _drag.SourceIndex;
        if (index < 0 || index >= _controller.Tabs.Count)
        {
            return;
        }
        var label = _controller.Tabs[index];
        try
        {
            var (host, _) = _controller.DetachTab(index, follow: true);
            _tornOffActive = true;
            _tornOffHost = host;
            _tornOffTab = label;
            _drag.MarkTornOff();
            Console.WriteLine($"Radiant.Host: live tore tab '{label}' into following window '{host}'");
        }
#pragma warning disable CA1031 // A tear-off failure must not crash the host UI; the drag simply continues.
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Radiant.Host: live tear-off failed: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// On releasing a live-tear-off drag, re-merge the torn tab into the host whose tab <b>strip</b> the
    /// cursor is over (any host except the follower itself, including this source host) — a drop over a
    /// window's body or empty space leaves it standalone. Strip-only matches the live hover preview
    /// (placeholder) and Chrome. The re-merge is performed by the follower (<c>tab.handoff</c>) so there is
    /// a single ownership transition; a now-empty follower self-closes.
    /// </summary>
    private void HandleTornOffRelease(RadiantApplication app)
    {
        var target = ResolveReMergeStrip(app);
        if (target is not null)
        {
            try
            {
                var p = new System.Text.Json.Nodes.JsonObject
                {
                    ["name"] = _tornOffTab,
                    ["target"] = target,
                    ["cursorX"] = PolledGlobalCursor(app).X,
                };
                new CommandClient(_tornOffHost).Send("tab.handoff", p.ToJsonString(), timeoutMs: 3000);
                Console.WriteLine($"Radiant.Host: re-merged torn tab '{_tornOffTab}' from '{_tornOffHost}' into '{target}'");
            }
#pragma warning disable CA1031 // A failed re-merge leaves the follower standalone; must not crash the host.
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Radiant.Host: re-merge failed: {ex.Message}");
            }
#pragma warning restore CA1031
        }
        ClearTornOff();
        _drag.Reset();
        _windowDragArmed = false;
        DragSession.Clear();
    }

    private void ClearTornOff()
    {
        _tornOffActive = false;
        _tornOffHost = "";
        _tornOffTab = "";
    }

    /// <summary>Other tab hosts the torn tab can re-merge into (every tab host except the follower itself,
    /// plus this source host), for cursor hit-testing during/at the end of a live-tear-off drag.</summary>
    private List<(string Host, WindowBounds Bounds)> ReMergeCandidates()
    {
        var candidates = OtherHostBounds()
            .Where(o => !string.Equals(o.Host, _tornOffHost, StringComparison.Ordinal))
            .ToList();
        if (_publishedBounds is { } self)
        {
            candidates.Add((_hostName, self));
        }
        return candidates;
    }

    /// <summary>
    /// The re-merge target host whose tab <b>strip</b> the cursor is over (any host except the follower,
    /// including this source host), or null. Strip-only: used both for the live hover placeholder and to
    /// resolve the drop on release, so a torn-off window only re-merges over a strip — not a window body.
    /// </summary>
    private string? ResolveReMergeStrip(RadiantApplication app)
    {
        var cursor = PolledGlobalCursor(app);
        return HostActions.ResolveStripTarget(ReMergeCandidates(), cursor.X, cursor.Y, StripProximityMargin);
    }

    /// <summary>
    /// When this host is a live-tear-off follower (<c>--follow</c>), track the cursor published in drag.json:
    /// move the window so the grab point stays under the cursor and reveal it, or hide it while the cursor is
    /// over a re-merge target strip (so that host's placeholder shows). Settle as a normal window once the
    /// drag ends (or no follow session arrives within the grace period).
    /// </summary>
    private void UpdateFollow(RadiantApplication app, double dt)
    {
        if (!_startHidden)
        {
            return;
        }
        var session = DragSession.Read();
        if (session is { Active: true, TornOff: true } && string.Equals(session.FollowHost, _hostName, StringComparison.Ordinal))
        {
            _followIdle = 0;
            // Toggle visibility by OPACITY, never SetVisible(false) after the first reveal: ordering this window
            // out mid-drag reshuffles the macOS key window and delivers the source host a spurious mouse-up that
            // commits the re-merge while the button is still held (you then can't drag back out). Opacity keeps
            // it on-screen + ordered-in. Position/opacity are set BEFORE the (idempotent) reveal so the window
            // never flashes at its spawn origin.
            if (session.OverStrip)
            {
                // Over a re-merge target strip: go transparent so that host shows our tab in its strip + body
                // instead. We own the torn tab's frame, so republish it as the live merge preview that host
                // reads (the window keeps rendering at opacity 0, so we still have a fresh frame each Render).
                app.SetOpacity(0f);
                _publishMergePreview = true;
            }
            else
            {
                app.MoveWindow((int)(session.CursorX - session.GrabOffsetX), (int)(session.CursorY - session.GrabOffsetY));
                app.SetOpacity(1f);
            }
            SetFollowVisible(app, true);
            return;
        }

        // No matching active follow session: the drag ended (or never arrived). Settle visible + become a
        // normal window. A definitive inactive session settles immediately; otherwise wait out the grace.
        _followIdle += dt;
        if (session is { Active: false } || _followIdle > FollowSettleSeconds)
        {
            SetFollowVisible(app, true);
            app.SetOpacity(1f); // restore from any transparent dock-preview state
            // Drop always-on-top + passthrough and take focus now the drag is over: the torn-off window becomes
            // the normal, front, active, mouse-interactive window. Focusing is safe here (post-release) — never
            // mid-drag.
            app.SetTopMost(false);
            app.SetMousePassthrough(false);
            app.Focus();
            _startHidden = false;
        }
    }

    private void SetFollowVisible(RadiantApplication app, bool visible)
    {
        if (_followShown == visible)
        {
            return;
        }
        app.SetVisible(visible);
        _followShown = visible;
    }

    /// <summary>Publish the live drag state to <c>drag.json</c> so the floating overlay / target host can react.</summary>
    private void PublishDragSession(RadiantApplication app, bool overStrip)
    {
        if (!_drag.Dragging)
        {
            return;
        }
        // Once torn off, the dragged tab is no longer in our strip — publish from the cached tear-off name
        // (indexing _controller.Tabs would be stale/out of range) and carry the follow fields.
        var name = _tornOffActive
            ? _tornOffTab
            : (_drag.SourceIndex >= 0 && _drag.SourceIndex < _controller.Tabs.Count ? _controller.Tabs[_drag.SourceIndex] : "");
        // The host that owns the dragged tab's frame (and thus publishes the merge preview): once torn off
        // it is the follower; otherwise this source host.
        var contentHost = _tornOffActive ? _tornOffHost : _hostName;
        var cursor = PolledGlobalCursor(app);
        DragSession.Write(new DragSessionState
        {
            Active = true,
            CursorX = cursor.X,
            CursorY = cursor.Y,
            TabName = name,
            FramesPath = name.Length > 0 ? TabController.FramesPath(name) : "",
            // The overlay reads the host-published thumbnail buffer (not the renderer's single-consumer
            // frame buffer). It may be a frame behind (written in Render, after this Update) or absent on
            // the first drag frame — the overlay falls back to the labelled chip until it can be read.
            ThumbnailPath = ThumbPath,
            SourceHost = _hostName,
            Label = _tornOffActive ? _tornOffTab : _drag.Label,
            OverStrip = overStrip,
            // Authoritative live-merge target + the preview buffer it reads. Only set when over another
            // host's strip (a cross-window merge), so a plain reorder over our own strip carries neither.
            TargetHost = _dragTargetHost,
            PreviewPath = _dragTargetHost.Length > 0 ? MergePreviewPath(contentHost) : "",
            TornOff = _tornOffActive,
            FollowHost = _tornOffActive ? _tornOffHost : "",
            GrabOffsetX = _tornOffActive ? TearOffGrabOffsetX : 0f,
            GrabOffsetY = _tornOffActive ? HostCompositor.StripHeight / 2f : 0f,
        });
    }

    /// <summary>The host-owned full-res merge-preview buffer path for <paramref name="host"/> — the target
    /// reads it to composite the dragged tab's live content during a cross-window merge hover.</summary>
    private static string MergePreviewPath(string host) =>
        Path.Combine(InstanceRegistry.RootDir, host, "merge-preview.bin");

    /// <summary>
    /// Republish the dragged (active) tab's latest frame — already in <see cref="_frameScratch"/> from this
    /// frame's compositing read — at full resolution into this host's own merge-preview buffer, for the
    /// target host to composite as the incoming tab's live content. A clean second
    /// single-producer/single-consumer channel (this host writes, the target reads), so it never contends
    /// with the renderer's frame buffer. The buffer is sized to the frame and recreated when its dims grow.
    /// </summary>
    private void PublishMergePreview()
    {
        if (_frameW <= 0 || _frameH <= 0)
        {
            return; // no frame composited yet — the target falls back to the translucent placeholder
        }
        var bytes = _frameW * _frameH * 4;
        try
        {
            if (_previewWriter is null || _previewWriterW != _frameW || _previewWriterH != _frameH)
            {
                _previewWriter?.Dispose();
                _previewWriter = SharedFrameBuffer.CreateWriter(MergePreviewPath(_hostName), _frameW, _frameH);
                _previewWriterW = _frameW;
                _previewWriterH = _frameH;
            }
            _previewWriter.Write(_frameScratch.AsSpan(0, bytes), _frameW, _frameH, FrameFormat.Bgra8Unorm);
        }
#pragma warning disable CA1031 // A preview-publish failure must not crash the host; the target falls back to the placeholder.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DisposePreviewWriter();
        }
#pragma warning restore CA1031
    }

    private void DisposePreviewWriter()
    {
        _previewWriter?.Dispose();
        _previewWriter = null;
        _previewWriterW = 0;
        _previewWriterH = 0;
    }

    /// <summary>
    /// Read <c>drag.json</c> each frame to detect a drag from another host whose cursor the drag driver has
    /// resolved to OUR strip (<see cref="DragSessionState.TargetHost"/> is authoritative — already excludes
    /// the source / follower and picks a single host even when windows overlap, so the preview buffer has
    /// exactly one reader). Sets <see cref="_incomingGap"/> (-1 = none), <see cref="_incomingLabel"/>, and
    /// <see cref="_incomingPreviewPath"/> (the buffer to read the incoming tab's live content from).
    /// </summary>
    private void UpdateIncomingDrag()
    {
        _incomingGap = -1;
        _incomingLabel = "";
        _incomingPreviewPath = "";
        if (DragSession.Read() is not { Active: true } session
            || !string.Equals(session.TargetHost, _hostName, StringComparison.Ordinal)
            || _publishedBounds is not { } bounds)
        {
            return;
        }
        // The drop slot, computed against our CURRENT tab count (matches TabController.ResolveAdoptSlot), so
        // the previewed insertion gap is exactly where AdoptTab will insert on release.
        var layout = new TabStripLayout(bounds.Width, Math.Max(_controller.Tabs.Count, 1));
        _incomingGap = layout.InsertionIndexAt(session.CursorX - bounds.X);
        _incomingLabel = session.Label;
        _incomingPreviewPath = session.PreviewPath;
    }

    /// <summary>The host-owned buffer path the dragged tab's thumbnail is published to (overlay reads it).</summary>
    private string ThumbPath => Path.Combine(InstanceRegistry.RootDir, _hostName, "drag-thumb.bin");

    /// <summary>
    /// Downscale the dragged (active) tab's latest frame — already in <see cref="_frameScratch"/> from this
    /// frame's compositing read — into the host-owned thumbnail buffer the floating overlay consumes. This
    /// is a clean second single-producer/single-consumer channel (host writes, overlay reads), so it never
    /// contends with the renderer's frame buffer (which the host reads as the active tab's single consumer).
    /// </summary>
    private void PublishDragThumbnail()
    {
        if (_frameW <= 0 || _frameH <= 0)
        {
            return; // no frame composited yet — overlay shows the chip
        }
        var (w, h) = ThumbnailScaler.Fit(_frameW, _frameH, ThumbMaxWidth, ThumbMaxHeight);
        _thumbScratch ??= new byte[ThumbMaxWidth * ThumbMaxHeight * 4];
        ThumbnailScaler.Downscale(_frameScratch, _frameW, _frameH, _thumbScratch, w, h);
        try
        {
            _thumbWriter ??= SharedFrameBuffer.CreateWriter(ThumbPath, ThumbMaxWidth, ThumbMaxHeight);
            _thumbWriter.Write(_thumbScratch.AsSpan(0, w * h * 4), w, h, FrameFormat.Bgra8Unorm);
        }
#pragma warning disable CA1031 // A thumbnail-publish failure must not crash the host UI; the overlay falls back to the chip.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DisposeThumbWriter();
        }
#pragma warning restore CA1031
    }

    private void DisposeThumbWriter()
    {
        _thumbWriter?.Dispose();
        _thumbWriter = null;
    }

    /// <summary>
    /// Ensure exactly one floating drag-overlay process is running (primary host only, lock-arbitrated).
    /// On by default; set <c>&lt;prefix&gt;_DRAG_OVERLAY=0</c> to disable (kill-switch for platforms where the
    /// macOS-verified focus behaviour is untested — Linux/Windows). The overlay previously broke the drag,
    /// but the cause was NOT passthrough (which is honoured — see
    /// <c>RadiantApplication.TrySetMousePassthrough</c>): revealing the overlay made it the macOS key
    /// window and stole focus from the host, delivering a spurious mouse-up that ended the drag. Fixed by
    /// <c>GLFW_FOCUS_ON_SHOW=false</c> (<c>RadiantWindowStyle.FocusOnShow</c>). Reorder / tear-off /
    /// cross-window merge-on-drop all work regardless via the in-window ghost (see TABS_ROADMAP.md).
    /// </summary>
    private void EnsureDragOverlay()
    {
        if (!TabOwnership.IsPrimary(_hostName)
            || RadiantAppIdentity.Current.EnvironmentIs("DRAG_OVERLAY", "0"))
        {
            return;
        }
        var lockPath = Path.Combine(Directory.GetParent(InstanceRegistry.RootDir)!.FullName, "overlay.lock");
        HostLock.EnsureSingleHost(lockPath, OverlayAlive, InstanceRegistry.IsAlive, SpawnDragOverlay);
    }

    private static bool OverlayAlive()
    {
        foreach (var info in InstanceRegistry.ListInstances())
        {
            if (Array.IndexOf(info.Capabilities, "drag-overlay") >= 0 && InstanceRegistry.IsAlive(info.Pid))
            {
                return true;
            }
        }
        return false;
    }

    private static int SpawnDragOverlay()
    {
        // Re-launch *this* binary as the floating overlay; see SelfRelaunch for why a hard-coded `dotnet`
        // host broke published builds ("dotnet---type does not exist").
        var psi = SelfRelaunch.BuildStartInfo("--type", "drag-overlay");
        var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to launch drag overlay.");
        Console.WriteLine("Radiant.Host: launched floating drag-overlay process.");
        return proc.Id;
    }

    /// <summary>Publish this host's active tab name to <c>active-tab.txt</c>, but only when it changed.</summary>
    private void PublishActiveTab()
    {
        var active = _controller.ActiveName;
        if (!string.Equals(active, _publishedActiveTab, StringComparison.Ordinal))
        {
            HostActiveTab.Write(_hostName, active);
            _publishedActiveTab = active;
        }
    }

    /// <summary>
    /// Keep all running hosts under a single macOS Dock tile. On the first frame this host either claims
    /// Dock ownership (single tile: keep the Regular policy, brand the icon, install the per-host Dock menu)
    /// or drops to an accessory app (no tile). Thereafter the owner keeps the menu installed, and a non-owner
    /// re-elects itself if the current owner dies — so the lone tile never disappears while any host lives.
    /// macOS only; set <c>&lt;prefix&gt;_DOCK_CONSOLIDATE=0</c> to restore the legacy one-tile-per-host behaviour.
    /// </summary>
    private void UpdateDockPresence(double dt)
    {
        if (!MacObjc.IsMac)
        {
            return;
        }

        if (RadiantAppIdentity.Current.EnvironmentIs("DOCK_CONSOLIDATE", "0"))
        {
            if (!_dockPresenceApplied)
            {
                _dockPresenceApplied = true;
                MacDockIcon.TrySet(); // legacy: every host brands its own tile
            }
            return;
        }

        if (!_dockPresenceApplied)
        {
            _dockPresenceApplied = true;
            if (DockOwnerLock.TryAcquire())
            {
                BecomeDockOwner();
            }
            else
            {
                MacDockIcon.TrySetAccessoryPolicy();
            }
            return;
        }

        if (_isDockOwner)
        {
            // Idempotent; covers a delegate that wasn't ready on the very first frame.
            MacDockMenu.TryInstall();
            return;
        }

        // Re-election: a non-owner periodically checks whether the Dock owner died and, if so, promotes
        // itself so the single tile survives.
        _dockReelectAccumulator += dt;
        if (_dockReelectAccumulator > 1.0)
        {
            _dockReelectAccumulator = 0;
            if (DockOwnerLock.TryAcquire())
            {
                BecomeDockOwner();
            }
        }
    }

    private void BecomeDockOwner()
    {
        _isDockOwner = true;
        MacDockIcon.TrySetRegularPolicy(); // no-op if already Regular (the original owner); promotes a survivor
        MacDockIcon.TrySet();              // brand the (now visible) tile
        MacDockMenu.TryInstall();
    }

    /// <summary>
    /// The screen-space cursor from the POLLED OS position (<see cref="RadiantApplication.PolledCursorPosition"/>
    /// = glfwGetCursorPos) rather than the event-fed <see cref="RadiantApplication.GlobalCursor"/>. The
    /// mouse-move event is unreliable during a button-held drag on macOS — and goes stale/jumpy once the
    /// cursor crosses onto another window — so all drag-driving reads use this to track the cursor smoothly
    /// over foreign windows and to avoid self-move feedback when this window is the one being moved.
    /// </summary>
    private static Vector2 PolledGlobalCursor(RadiantApplication app) =>
        new(app.WindowX + app.PolledCursorPosition.X, app.WindowY + app.PolledCursorPosition.Y);

    /// <summary>
    /// Resolve which other host's tab <b>strip</b> the cursor is over, for a cross-window merge on release.
    /// Strip-only: a drop over a target window's body (away from its tabs) does not merge, matching the
    /// per-frame placeholder preview and the tear-off re-merge. Null when over no other host's strip.
    /// </summary>
    private string? ResolveStripMergeTarget(RadiantApplication app)
    {
        var cursor = PolledGlobalCursor(app);
        return HostActions.ResolveStripTarget(OtherHostBounds(), cursor.X, cursor.Y, StripProximityMargin);
    }

    /// <summary>
    /// The other tab-capable host windows' published rects (excluding self), for cursor hit-testing during
    /// a drag (merge target on drop, strip-placeholder target per-frame).
    /// </summary>
    private List<(string Host, WindowBounds Bounds)> OtherHostBounds()
    {
        var others = new List<(string Host, WindowBounds Bounds)>();
        foreach (var info in InstanceRegistry.ListInstances())
        {
            if (string.Equals(info.Name, _hostName, StringComparison.Ordinal)
                || Array.IndexOf(info.Capabilities, "tab") < 0)
            {
                continue;
            }
            if (HostWindowBounds.Read(info.Name) is { } bounds)
            {
                others.Add((info.Name, bounds));
            }
        }
        return others;
    }

    /// <summary>
    /// Hand the tab at <paramref name="index"/> over to <paramref name="target"/> via its <c>tab.adopt</c>
    /// action (the cross-process inverse of tear-off): the target takes ownership + inserts at the drop
    /// slot, and we drop it locally on success. On failure we keep it (a rescan re-adopts it since the
    /// owner marker is unchanged).
    /// </summary>
    private void TryMerge(RadiantApplication app, int index, string target)
    {
        if (index < 0 || index >= _controller.Tabs.Count)
        {
            return;
        }
        var name = _controller.Tabs[index];
        var cursor = PolledGlobalCursor(app);
        // The renderer's frame buffer is single-consumer: if we still hold it open when the target opens it,
        // the two readers contend and the target's first frames are starved → the tab blinks out for a moment
        // on drop. Release our reader BEFORE the handoff (only if it is the tab being merged — the active one)
        // so the target adopts a free buffer. The renderer keeps producing (newest-wins); nothing is lost.
        if (string.Equals(_activeReaderName, name, StringComparison.Ordinal))
        {
            DisposeReader();
        }
        try
        {
            var p = new System.Text.Json.Nodes.JsonObject { ["name"] = name, ["cursorX"] = cursor.X };
            var resp = new CommandClient(target).Send("tab.adopt", p.ToJsonString(), timeoutMs: 3000);
            if (string.Equals(resp.Status, "ok", StringComparison.Ordinal))
            {
                _controller.Forget(index);
                Console.WriteLine($"Radiant.Host: merged tab '{name}' into host '{target}'");
            }
            else
            {
                Console.Error.WriteLine($"Radiant.Host: merge of '{name}' into '{target}' failed: {resp.Error?.Message}");
            }
        }
#pragma warning disable CA1031 // A merge failure must not crash the host UI.
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Radiant.Host: merge failed: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    private void TryTearOff(int index)
    {
        if (index < 0 || index >= _controller.Tabs.Count)
        {
            return;
        }
        var label = _controller.Tabs[index];
        try
        {
            var (host, _) = _controller.DetachTab(index);
            Console.WriteLine($"Radiant.Host: tore tab '{label}' out into new window '{host}'");
        }
#pragma warning disable CA1031 // A tear-off failure must not crash the host UI.
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Radiant.Host: tear-off failed: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    private void DrainCommands()
    {
        if (_receiver is null)
        {
            return;
        }
        foreach (var cmd in _receiver.DrainPendingCommands())
        {
            _receiver.WriteResponse(_controller.Handle(cmd));
        }
    }

    /// <summary>
    /// Forward this frame's window input to the active tab's renderer via its <see cref="InputRing"/>.
    /// Mouse coordinates are translated into the renderer's content space (origin at the top-left of
    /// the area below the tab strip). Strip-area presses are suppressed so a tab click doesn't also
    /// reach the 3D viewport.
    /// </summary>
    private void ForwardInputToActiveTab(RadiantApplication app, bool pointerInStrip)
    {
        EnsureInputRings();

        var activeName = _controller.ActiveName;
        var activeInput = activeName is not null && _inputRings.TryGetValue(activeName, out var ring) ? ring : null;
        if (activeInput is null)
        {
            return;
        }

        var input = app.Input;
        var p = input.MousePosition;
        var contentX = p.X;
        var contentY = p.Y - HostCompositor.StripHeight;

        activeInput.SetMousePosition(contentX, contentY);
        activeInput.SetSize(_lastTabWidth, _lastTabHeight);
        // Device pixel ratio so the renderer renders at physical resolution (sharp on Retina) while
        // size/mouse stay logical. WindowWidth is logical, FramebufferWidth physical.
        var pixelScale = app.WindowWidth > 0 ? (float)app.FramebufferWidth / app.WindowWidth : 1f;
        activeInput.SetScale(pixelScale);
        activeInput.SetFocus(true);
        // Thread the host's real window-present cost back to the active tab so its --profile report can
        // attribute vsync time its own offscreen "present" (a map+publish) can't see. Only the active
        // tab is composited+presented, so only it gets a value; background tabs keep reading 0.
        activeInput.SetHostPresentMs((float)app.LastPresentMs, (int)_frameCount);

        foreach (var btn in s_forwardedButtons)
        {
            if (input.IsMouseButtonPressed(btn) && !pointerInStrip)
            {
                activeInput.Push(new InputEvent(InputEventKind.MouseDown, (int)btn, contentX, contentY));
            }
            // Always forward releases (even if the press began in the strip) so the renderer never
            // gets stuck with a button held.
            if (input.IsMouseButtonReleased(btn))
            {
                activeInput.Push(new InputEvent(InputEventKind.MouseUp, (int)btn, contentX, contentY));
            }
        }

        if (input.ScrollDelta != Vector2.Zero && !pointerInStrip)
        {
            activeInput.Push(new InputEvent(InputEventKind.Scroll, 0, input.ScrollDelta.X, input.ScrollDelta.Y));
        }

        // KEYS, WHICH USED TO REACH NO TAB AT ALL. Only Char was forwarded, and Char is the printable
        // character a key produced -- Tab, Return, the arrows and every modifier produce none. So a
        // renderer could read typing and could not read a shortcut, and Ergon.Studio's own G binding
        // had been dead code since it was written.
        //
        // The code on the wire is the Silk.NET Key value, matching s_forwardedButtons' contract that
        // the toolkit's own int is what a renderer reads.
        foreach (var key in s_forwardedKeys)
        {
            if (input.IsKeyPressed(key))
            {
                activeInput.Push(new InputEvent(InputEventKind.KeyDown, (int)key, 0f, 0f));
            }

            if (input.IsKeyReleased(key))
            {
                activeInput.Push(new InputEvent(InputEventKind.KeyUp, (int)key, 0f, 0f));
            }
        }

        if (input.LastCharacter is { } ch)
        {
            activeInput.Push(new InputEvent(InputEventKind.Char, ch, 0f, 0f));
        }
    }

    /// <summary>
    /// Maintain one <see cref="InputRing"/> writer per owned tab: drop rings for tabs that have gone
    /// away, lazily create one for each current tab, and mark exactly the active tab active (so every
    /// background tab's renderer pauses its viewport render). Forwarded input goes only to the active
    /// tab's ring (see <see cref="ForwardInputToActiveTab"/>).
    /// </summary>
    private void EnsureInputRings()
    {
        var tabs = _controller.Tabs;
        var activeName = _controller.ActiveName;

        // Drop our writer for any tab that has closed / detached (just unmaps — the ring's backing
        // input.bin lives in the renderer's instance dir and is reused in place if this tab is later
        // re-adopted, or removed with that dir when the renderer deregisters; the host never deletes it).
        if (_inputRings.Count > 0)
        {
            var stale = _inputRings.Keys.Where(name => !tabs.Contains(name)).ToList();
            foreach (var name in stale)
            {
                _inputRings[name].Dispose();
                _inputRings.Remove(name);
            }
        }

        foreach (var name in tabs)
        {
            if (!_inputRings.ContainsKey(name))
            {
                var path = Path.Combine(InstanceRegistry.RootDir, name, "input.bin");
                InputRing? created = null;
                try
                {
                    created = InputRing.CreateWriter(path);
                    _inputRings[name] = created;
                    created = null; // ownership transferred to _inputRings
                }
                catch (IOException)
                {
                    // buffer mid-creation / locked — retry next frame
                }
                finally
                {
                    created?.Dispose();
                }
            }
            if (_inputRings.TryGetValue(name, out var ring))
            {
                ring.SetActive(name == activeName);
            }
        }
    }

    private void DisposeInput()
    {
        foreach (var ring in _inputRings.Values)
        {
            ring.Dispose();
        }
        _inputRings.Clear();
    }

    private void Render(RadiantApplication app, Renderer2D renderer)
    {
        if (_frameCount++ == 0)
        {
            Console.WriteLine("Radiant.Host: first render frame (window + GPU initialised)");
            // The macOS Dock tile (brand vs. accessory) is now decided in Update via UpdateDockPresence so a
            // single tile spans all host processes; see UpdateDockPresence.
        }
        _font ??= LoadFont(renderer);

        EnsureActiveReader();

        // Drop the previous tab's texture on a switch so we never blit a stale frame for one frame.
        var activeName = _controller.ActiveName;
        if (activeName != _renderedTabName)
        {
            _tabTexture?.Dispose();
            _tabTexture = null;
            _renderedTabName = activeName;
        }

        if (_activeReader != null && _activeReader.TryRead(_frameScratch, out var info))
        {
            var bytes = info.Width * info.Height * 4;
            if (_tabTexture == null || _tabTexture.Width != info.Width || _tabTexture.Height != info.Height)
            {
                _tabTexture?.Dispose();
                // The renderer renders to a Bgra8UnormSrgb target and reads back the sRGB-ENCODED bytes
                // verbatim (see OffscreenPresentationSurface). The host's swapchain is also sRGB and
                // re-encodes on write, so the tab texture must be sampled as sRGB (decode → linear) to
                // avoid a double sRGB encode that washes the image out lighter. Hence Bgra8UnormSrgb,
                // not Bgra8Unorm.
                _tabTexture = Texture2D.Create(renderer, info.Width, info.Height, TextureFormat.Bgra8UnormSrgb);
            }
            _tabTexture.Update(_frameScratch.AsSpan(0, bytes));
            _frameW = info.Width;
            _frameH = info.Height;
        }

        // Tell the active renderer to size its content to the area below the strip.
        _lastTabWidth = app.WindowWidth > 0 ? app.WindowWidth : 1280;
        _lastTabHeight = Math.Max(1, (app.WindowHeight > 0 ? app.WindowHeight : 720 + (int)HostCompositor.StripHeight) - (int)HostCompositor.StripHeight);

        // RadiantApplication brackets this callback with BeginFrame()/EndFrame(); HostCompositor only
        // queues draws. Size the composite to the window.
        var viewW = app.WindowWidth > 0 ? app.WindowWidth : 1280;
        var viewH = app.WindowHeight > 0 ? app.WindowHeight : 720 + (int)HostCompositor.StripHeight;
        // During an in-strip reorder, render the packed preview (dragged tab lifted out + gap placeholder)
        // so it reads as relocating rather than duplicating.
        var reorder = _drag.Dragging && !WindowDragging && _liveState.Kind == LiveDragKind.ReorderInStrip
            ? new ReorderPreview(_drag.SourceIndex, _liveState.InsertionGap, _drag.Label)
            : (ReorderPreview?)null;
        // The pixel scale the subprocess rendered its frame at (= what we send it via the input ring), used
        // to composite that frame at its native 1:1 size so it doesn't jitter while the window resizes.
        var framePixelScale = app.WindowWidth > 0 ? (float)app.FramebufferWidth / app.WindowWidth : 1f;

        // A cross-window merge hovering our strip: composite the incoming tab's LIVE content (read from the
        // content host's preview buffer) into our body + a reflowed strip cell at the drop slot — previewing
        // the merge as already-done before release. Mutually exclusive with our own reorder (a host is either
        // the drag source reordering, or a merge target). Until the first preview frame is readable it falls
        // back to the translucent placeholder, so the strip is never blank.
        var incomingReady = _incomingGap >= 0 && _incomingPreviewPath.Length > 0 && UpdateIncomingPreviewTexture(renderer);
        if (_incomingGap < 0 || _incomingPreviewPath.Length == 0)
        {
            DisposePreviewReader();
        }

        var bodyTexture = incomingReady ? _previewTexture : _tabTexture;
        var incoming = incomingReady ? new IncomingPreview(_incomingGap, _incomingLabel) : (IncomingPreview?)null;
        // The preview frame (rendered at the source's size) is stretched to fill our body (framePixelScale 0
        // ⇒ stretch-to-fill in HostCompositor) so it reads as occupying this window regardless of the
        // source's window size; our own active frame keeps the native-1:1 anti-jitter path.
        var bodyScale = incomingReady ? 0f : framePixelScale;
        HostCompositor.Draw(renderer, bodyTexture, viewW, viewH, _controller.Tabs, _controller.ActiveIndex, _hoveredTab, reorder, bodyScale, _font, incoming);

        var stripLayout = new TabStripLayout(viewW, Math.Max(_controller.Tabs.Count, 1));

        // Incoming merge but the preview frame isn't readable yet (first frame / buffer mid-create): fall
        // back to the "potential tab" placeholder so the drop slot is still shown.
        if (_incomingGap >= 0 && !incomingReady)
        {
            HostCompositor.DrawTabPlaceholder(renderer, stripLayout, _incomingGap, _incomingLabel, _font);
        }

        // A sole-tab window-drag moves the whole window, so there is no ghost/placeholder to draw locally
        // (the window itself is the feedback). Otherwise the feedback depends on the live drag state.
        if (_drag.Dragging && !WindowDragging)
        {
            switch (_liveState.Kind)
            {
                case LiveDragKind.ReorderInStrip:
                    // The packed reorder preview (with the placeholder) was drawn by HostCompositor.Draw above.
                    DisposeThumbWriter();
                    break;
                case LiveDragKind.FloatingBelow:
                    // Floating below any strip: the in-window ghost + the floating overlay's live thumbnail.
                    HostCompositor.DrawDragGhost(renderer, _drag.Position, _font, _drag.Label);
                    PublishDragThumbnail();
                    break;
                case LiveDragKind.MergeOverStrip:
                    // Over another host's strip: that host draws the placeholder; nothing local, no thumbnail.
                    DisposeThumbWriter();
                    break;
                case LiveDragKind.TornOffFollowing:
                case LiveDragKind.Idle:
                default:
                    DisposeThumbWriter();
                    break;
            }
        }
        else
        {
            DisposeThumbWriter();
        }

        // Republish our active frame as the cross-window merge preview when we are the content owner over a
        // target strip: the source dragging over another host's strip, or the hidden follower over a re-merge
        // strip (both set _publishMergePreview in Update). Otherwise drop the writer.
        if (_publishMergePreview)
        {
            PublishMergePreview();
        }
        else
        {
            DisposePreviewWriter();
        }
    }

    private static MsdfFont LoadFont(Renderer2D renderer)
    {
        var font = MsdfFont.LoadEmbedded("default");
        renderer.RegisterMsdfFont(font);
        return font;
    }

    private void EnsureActiveReader()
    {
        var name = _controller.ActiveName;
        if (name is null)
        {
            DisposeReader();
            return;
        }
        if (_activeReaderName == name && _activeReader != null)
        {
            return;
        }

        DisposeReader();
        var path = TabController.FramesPath(name);
        if (File.Exists(path))
        {
            try
            {
                _activeReader = SharedFrameBuffer.OpenReader(path);
                _activeReaderName = name;
            }
            // IOException: buffer mid-creation / locked. InvalidData/NotSupported: a renderer wrote an
            // incompatible buffer (wrong magic / protocol version) — skip rather than crash; the
            // version filter in TabController.Refresh normally excludes these before we get here.
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
            {
                _activeReader = null;
                _activeReaderName = null;
            }
        }
    }

    private void DisposeReader()
    {
        _activeReader?.Dispose();
        _activeReader = null;
        _activeReaderName = null;
    }

    /// <summary>
    /// Open (lazily, reopening on a path change) the content host's merge-preview buffer and pull its latest
    /// frame into <see cref="_previewTexture"/>. Returns true once a preview frame is available (this read or
    /// a recent one held while a newer frame isn't yet published) so the body can show the incoming tab's
    /// live content; false until the buffer exists and yields a first frame (caller falls back to the
    /// placeholder).
    /// </summary>
    private bool UpdateIncomingPreviewTexture(Renderer2D renderer)
    {
        EnsurePreviewReader();
        if (_previewReader is null)
        {
            return false;
        }
        _previewScratch ??= new byte[MaxFrameBytes];
        if (_previewReader.TryRead(_previewScratch, out var info))
        {
            var bytes = info.Width * info.Height * 4;
            if (_previewTexture is null || _previewTexture.Width != info.Width || _previewTexture.Height != info.Height)
            {
                _previewTexture?.Dispose();
                // The preview carries the renderer's sRGB-encoded bytes (same convention as _tabTexture), so
                // sample as Bgra8UnormSrgb to avoid a double sRGB encode.
                _previewTexture = Texture2D.Create(renderer, info.Width, info.Height, TextureFormat.Bgra8UnormSrgb);
            }
            _previewTexture.Update(_previewScratch.AsSpan(0, bytes));
        }
        return _previewTexture is not null;
    }

    private void EnsurePreviewReader()
    {
        if (_previewReaderPath == _incomingPreviewPath && _previewReader != null)
        {
            return;
        }
        DisposePreviewReader();
        if (_incomingPreviewPath.Length == 0 || !File.Exists(_incomingPreviewPath))
        {
            return;
        }
        try
        {
            _previewReader = SharedFrameBuffer.OpenReader(_incomingPreviewPath);
            _previewReaderPath = _incomingPreviewPath;
        }
        // IOException: buffer mid-creation / locked. InvalidData/NotSupported: an unexpected layout — skip;
        // the placeholder fallback covers the gap until it is readable.
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
        {
            _previewReader = null;
            _previewReaderPath = null;
        }
    }

    private void DisposePreviewReader()
    {
        _previewReader?.Dispose();
        _previewReader = null;
        _previewReaderPath = null;
        _previewTexture?.Dispose();
        _previewTexture = null;
    }

    public void Dispose()
    {
        _receiver?.Dispose();
        _receiver = null;
        DisposeReader();
        DisposeInput();
        DisposeThumbWriter();
        DisposePreviewWriter();
        DisposePreviewReader();
        _tabTexture?.Dispose();
        _tabTexture = null;
        _font?.Dispose();
        _font = null;
    }
}
