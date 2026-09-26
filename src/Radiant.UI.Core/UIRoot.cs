using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Radiant.Graphics2D;
using Radiant.Platform;
using Radiant.Text;
using static Facebook.Yoga.YGNodeAPI;
using static Facebook.Yoga.YGNodeStyleAPI;

namespace Radiant.UI.Core;

/// <summary>
/// A tree of UI: the elements it was given, kept live. Each frame, in order:
/// <list type="number">
/// <item>input is dispatched (<see cref="PointerDown"/>, <see cref="KeyDown"/>, …) against the last layout;</item>
/// <item><see cref="Update"/> rebuilds what changed, lays out what moved, and runs effects;</item>
/// <item><see cref="Paint"/> draws the tree.</item>
/// </list>
/// Everything happens on one thread, the UI thread.
/// </summary>
public sealed class UIRoot : IDisposable
{
    private const int MaxBuildPasses = 100;
    private const double DoubleClickSeconds = 0.5;
    private const float DoubleClickDistance = 4f;

    private readonly ElementNode _root;
    private readonly HashSet<ElementNode> _dirty = [];
    private readonly List<EffectHook> _effects = [];
    private readonly HashSet<ScrollRenderNode> _scrollers = [];
    private readonly HashSet<GridRenderNode> _grids = [];
    private readonly HashSet<Facebook.Yoga.Node> _hugs = [];
    private readonly HashSet<ScrollRenderNode> _animating = [];
    private readonly List<PortalRenderNode> _portals = [];
    private readonly List<TickerEntry> _tickers = [];
    private readonly System.Threading.Lock _busyLock = new();
    private readonly List<string> _busy = [];
    // Kept most specific last: by depth, then in the order added.
    private readonly List<(KeyChord Chord, Func<bool> Run, int Depth)> _shortcuts = [];
    private readonly List<BoxRenderNode> _focusTraps = [];
    private readonly List<Action<PointerDownObservation>> _pointerDownObservers = [];
    private bool _portalsChanged;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private List<RenderNode> _hovered = [];
    private List<RenderNode>? _pressed;
    private RenderNode? _focused;
    private bool _focusVisible;
    private (double Time, Vector2 Position, PointerButton Button, int Count) _lastClick;
    private bool _mounted;
    private ITextInputClient? _textInputClient;

    /// <summary>A tree showing <paramref name="element"/>.</summary>
    /// <param name="element">What to show.</param>
    /// <param name="fonts">Where text finds its fonts; <see cref="FontLibrary.Default"/> if null.</param>
    public UIRoot(Element element, FontLibrary? fonts = null)
    {
        Fonts = fonts ?? FontLibrary.Default;
        Clock = () => _clock.Elapsed.TotalSeconds;
        _root = new ElementNode(this, null, new RootElement(element), 0);
    }

    /// <summary>The fonts text is set in.</summary>
    public FontLibrary Fonts { get; }

    /// <summary>Called when something changes that needs a new frame (state set, a signal changed).</summary>
    public Action? FrameRequested { get; set; }

    /// <summary>Whether anything is waiting to be rebuilt or run.</summary>
    public bool NeedsUpdate => !_mounted || _dirty.Count > 0 || _effects.Count > 0 || _animating.Count > 0 || _tickers.Count > 0;

    /// <summary>
    /// Whether the UI has settled: mounted, nothing to rebuild or run, no scroll moving by itself, no
    /// <see cref="TickerKind.Animation"/> ticker, and no work begun with <see cref="BeginBusy"/> or
    /// <see cref="TrackBusy"/> unfinished. Spinners (<see cref="TickerKind.Continuous"/>) and waits
    /// (<see cref="TickerKind.Timer"/>) don't count. Tests and agents act when it's true.
    /// </summary>
    public bool IsIdle
    {
        get
        {
            if (!_mounted || _dirty.Count > 0 || _effects.Count > 0 || _animating.Count > 0)
            {
                return false;
            }
            foreach (var ticker in _tickers)
            {
                if (ticker.Kind == TickerKind.Animation)
                {
                    return false;
                }
            }
            lock (_busyLock)
            {
                return _busy.Count == 0;
            }
        }
    }

    /// <summary>Why <see cref="IsIdle"/> is false, one reason each; empty when it's true.</summary>
    public IReadOnlyList<string> BusyReasons()
    {
        var reasons = new List<string>();
        if (!_mounted)
        {
            reasons.Add("not mounted yet");
        }
        if (_dirty.Count > 0)
        {
            reasons.Add($"{_dirty.Count} component(s) to rebuild");
        }
        if (_effects.Count > 0)
        {
            reasons.Add($"{_effects.Count} effect(s) to run");
        }
        foreach (var scroller in _animating)
        {
            reasons.Add($"scroll area #{scroller.Id} moving");
        }
        foreach (var ticker in _tickers)
        {
            if (ticker.Kind == TickerKind.Animation)
            {
                reasons.Add("animation: " + ticker.Reason);
            }
        }
        lock (_busyLock)
        {
            foreach (var reason in _busy)
            {
                reasons.Add("busy: " + reason);
            }
        }
        return reasons;
    }

    /// <summary>The reasons given for the tickers of <paramref name="kind"/> now running.</summary>
    public IReadOnlyList<string> RunningTickers(TickerKind kind) =>
        [.. _tickers.Where(t => t.Kind == kind).Select(t => t.Reason)];

    /// <summary>
    /// Marks the UI busy (not <see cref="IsIdle"/>) with work outside it, such as a request it's waiting
    /// on, until the result is disposed. From any thread.
    /// </summary>
    public IDisposable BeginBusy(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        lock (_busyLock)
        {
            _busy.Add(reason);
        }
        return new BusyToken(this, reason);
    }

    /// <summary>Marks the UI busy until <paramref name="task"/> finishes. From any thread.</summary>
    public void TrackBusy(System.Threading.Tasks.Task task, string reason)
    {
        ArgumentNullException.ThrowIfNull(task);
        var token = BeginBusy(reason);
        task.ContinueWith(_ => token.Dispose(), System.Threading.CancellationToken.None,
            System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously, System.Threading.Tasks.TaskScheduler.Default);
    }

    private void EndBusy(string reason)
    {
        lock (_busyLock)
        {
            _busy.Remove(reason);
        }
        // The UI may now be idle, which whoever waits for it should see.
        FrameRequested?.Invoke();
    }

    private sealed class BusyToken(UIRoot root, string reason) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                root.EndBusy(reason);
            }
        }
    }

    private sealed record TickerEntry(Action<double> Tick, TickerKind Kind, string Reason);

    /// <summary>
    /// Raised with each input the root is given, before it's dispatched: for recording what happened.
    /// </summary>
    public event Action<UIInputEvent>? InputReceived;

    /// <summary>The size of the last layout.</summary>
    public Vector2 Size { get; private set; }

    /// <summary>Seconds since some fixed moment; replaceable for tests. Used to spot double clicks.</summary>
    public Func<double> Clock { get; set; }

    /// <summary>Whether focus was last moved by the keyboard, so the focused box should show a focus ring.</summary>
    public bool IsFocusVisible => _focused is not null && _focusVisible;

    /// <summary>
    /// The pointer's shape: the <see cref="Box.Cursor"/> of the deepest box under the pointer
    /// that sets one (of the pressed box's path while a press is held), or the arrow.
    /// <see cref="RadiantUI"/> shows it through the platform.
    /// </summary>
    public CursorShape Cursor { get; private set; }

    /// <summary>The <see cref="Cursor"/> changed.</summary>
    public event Action<CursorShape>? CursorChanged;

    /// <summary>
    /// Where typed text goes: a text field sets itself here while it has focus, and clears it
    /// when it loses focus. <see cref="RadiantUI"/> hands it to the platform's text input, which
    /// then delivers committed and composed (input method) text to it rather than as
    /// <see cref="TextInput"/> events.
    /// </summary>
    public ITextInputClient? TextInputClient
    {
        get => _textInputClient;
        set
        {
            if (!ReferenceEquals(_textInputClient, value))
            {
                _textInputClient = value;
                TextInputClientChanged?.Invoke(value);
            }
        }
    }

    /// <summary>The <see cref="TextInputClient"/> changed.</summary>
    public event Action<ITextInputClient?>? TextInputClientChanged;

    internal RenderNode RootRenderNode => _root.RenderNode!;

    internal RenderNode? FocusedNode => _focused;

    /// <summary>Shows a different element, keeping the state of whatever matches the old one.</summary>
    public void SetRoot(Element element)
    {
        if (!_mounted)
        {
            _root.Element = new RootElement(element);
            return;
        }
        UpdateNode(_root, new RootElement(element));
    }

    /// <summary>
    /// Brings the tree up to date for a frame of <paramref name="size"/>: rebuilds what changed,
    /// lays out, and runs the effects the builds scheduled (repeating if those change state).
    /// </summary>
    public void Update(Vector2 size)
    {
        if (!_mounted)
        {
            Mount(_root);
            _mounted = true;
        }
        for (var pass = 0; ; pass++)
        {
            FlushBuild();
            Layout(size);
            if (_effects.Count == 0)
            {
                break;
            }
            if (pass == MaxBuildPasses)
            {
                throw new InvalidOperationException("Effects kept changing state: an update loop.");
            }
            FlushEffects();
        }
        // A hovered box's cursor may have changed with its props.
        RefreshCursor();
    }

    /// <summary>
    /// Advances what moves by itself (scroll momentum and bounce) by <paramref name="seconds"/>.
    /// Call once a frame, before <see cref="Update"/>.
    /// </summary>
    public void Advance(double seconds)
    {
        foreach (var ticker in _tickers.ToArray())
        {
            ticker.Tick(seconds);
        }
        foreach (var scroller in _animating.ToArray())
        {
            if (!scroller.Advance(seconds))
            {
                _animating.Remove(scroller);
            }
        }
        if (_animating.Count > 0)
        {
            FrameRequested?.Invoke();
        }
    }

    /// <summary>
    /// Draws the tree. The renderer's frame must have been begun with its attachment size
    /// (<see cref="Renderer2D.BeginFrame(uint, uint, float)"/>), which clips and opacity need.
    /// </summary>
    /// <summary>How many nodes the last <see cref="Paint"/> drew: nodes out of view aren't.</summary>
    internal int LastPainted { get; private set; }

    public void Paint(Renderer2D renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        if (_mounted)
        {
            var context = new PaintContext(renderer, Size);
            context.Paint(RootRenderNode);
            LastPainted = context.Painted;
        }
    }

    // ------------------------------------------------------------------ building

    internal void MarkDirty(ElementNode node)
    {
        if (!node.Mounted)
        {
            return;
        }
        node.Dirty = true;
        if (_dirty.Add(node))
        {
            FrameRequested?.Invoke();
        }
    }

    internal void QueueEffect(EffectHook effect)
    {
        _effects.Add(effect);
        FrameRequested?.Invoke();
    }

    private void FlushBuild()
    {
        for (var pass = 0; _dirty.Count > 0; pass++)
        {
            if (pass == MaxBuildPasses)
            {
                throw new InvalidOperationException("State kept changing while building: an update loop.");
            }
            var batch = new List<ElementNode>(_dirty);
            _dirty.Clear();
            // Parents first: rebuilding a parent may rebuild (and clean) a dirty child on the way.
            batch.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            foreach (var node in batch)
            {
                if (node.Mounted && node.Dirty)
                {
                    // A component rebuilt on its own (its state changed) is caught by the boundary
                    // above it, as it would be when built from there.
                    try
                    {
                        Render(node, node.Element);
                        SyncRenderChildren(node.NearestHost());
                    }
                    catch (Exception error) when (error is not OutOfMemoryException && BoundaryAbove(node) is not null)
                    {
                        Catch(BoundaryAbove(node)!, error);
                    }
                }
            }
        }
        if (_portalsChanged)
        {
            _portalsChanged = false;
            SyncRenderChildren(_root);
        }
    }

    private void FlushEffects()
    {
        // Children's effects before their parents', as a parent's effect may rely on its children
        // being ready; a component's own effects in the order it declared them (the sort is stable).
        var effects = _effects.OrderByDescending(e => e.Owner.Depth).ToArray();
        _effects.Clear();
        foreach (var effect in effects)
        {
            if (effect.Owner.Mounted)
            {
                effect.Run();
            }
        }
    }

    private ElementNode Mount(Element element, ElementNode parent, int slot)
    {
        var node = new ElementNode(this, parent, element, slot);
        Mount(node);
        return node;
    }

    private void Mount(ElementNode node)
    {
        node.Mounted = true;
        if (node.Element is HostElement host)
        {
            node.RenderNode = host.CreateRenderNode();
            node.RenderNode.Owner = node;
            node.RenderNode.Update(host, null);
        }
        Render(node, null);
    }

    /// <summary>Gives a node a new element and brings its subtree up to date, unless nothing changed.</summary>
    private void UpdateNode(ElementNode node, Element element)
    {
        var previous = node.Element;
        if (!node.Dirty && Equals(previous, element))
        {
            return;
        }
        node.Element = element;
        if (element is HostElement host)
        {
            node.RenderNode!.Update(host, previous as HostElement);
        }
        Render(node, previous);
    }

    private void Render(ElementNode node, Element? previous)
    {
        switch (node.Element)
        {
            case ErrorBoundary when node.Caught is null:
                // What the content throws as it builds is caught here, and the fallback shows instead.
                try
                {
                    BuildComponent(node);
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    Catch(node, error);
                }
                break;
            case Component:
                BuildComponent(node);
                break;
            case HostElement host:
                Reconcile(node, host.ChildElements);
                SyncRenderChildren(node);
                break;
            case Fragment fragment:
                Reconcile(node, fragment.Children);
                break;
            case IProvider provider:
                if (previous is IProvider old && !provider.ProvidesSameValueAs(old) && node.Consumers is { } consumers)
                {
                    foreach (var consumer in consumers)
                    {
                        MarkDirty(consumer);
                    }
                }
                Reconcile(node, [provider.Child]);
                break;
            default:
                throw new InvalidOperationException($"{node.Element.GetType().Name} is not a kind of element the UI knows how to show.");
        }
    }

    private void BuildComponent(ElementNode node)
    {
        node.Dirty = false;
        var context = node.Context ??= new BuildContext(node);
        context.BeginBuild();
        var child = ((Component)node.Element).Build(context);
        context.EndBuild();
        Reconcile(node, [child]);
    }

    /// <summary>
    /// An error boundary's content threw: what was built of it goes, and the boundary builds again,
    /// showing its fallback until it's reset.
    /// </summary>
    private void Catch(ElementNode boundary, Exception error)
    {
        foreach (var child in boundary.Children)
        {
            Unmount(child);
        }
        boundary.Children = [];
        boundary.Caught = error;
        ((ErrorBoundary)boundary.Element).OnError?.Invoke(error);
        BuildComponent(boundary);
        SyncRenderChildren(boundary.NearestHost());
    }

    /// <summary>Clears what an error boundary caught, so its content is built again.</summary>
    internal void ResetBoundary(ElementNode boundary)
    {
        if (boundary.Mounted && boundary.Caught is not null)
        {
            boundary.Caught = null;
            MarkDirty(boundary);
        }
    }

    // The nearest error boundary above a node that isn't already showing its fallback.
    private static ElementNode? BoundaryAbove(ElementNode node)
    {
        for (var at = node.Parent; at is not null; at = at.Parent)
        {
            if (at.Element is ErrorBoundary && at.Caught is null && at.Mounted)
            {
                return at;
            }
        }
        return null;
    }

    /// <summary>
    /// Matches a node's new child elements to its current children: by key where elements have
    /// one, otherwise by position and type. Matches are updated; the rest are mounted or unmounted.
    /// </summary>
    private void Reconcile(ElementNode parent, IReadOnlyList<Element?> elements)
    {
        Dictionary<Key, ElementNode>? keyed = null;
        Dictionary<int, ElementNode>? positional = null;
        foreach (var child in parent.Children)
        {
            if (child.Element.Key is { } key)
            {
                (keyed ??= [])[key] = child;
            }
            else
            {
                (positional ??= [])[child.Slot] = child;
            }
        }

        List<ElementNode>? discarded = null;
        var next = new List<ElementNode>(elements.Count);
        for (var i = 0; i < elements.Count; i++)
        {
            if (elements[i] is not { } element)
            {
                continue;
            }
            ElementNode? candidate = null;
            if (element.Key is { } key)
            {
                keyed?.Remove(key, out candidate);
            }
            else
            {
                positional?.Remove(i, out candidate);
            }

            ElementNode node;
            if (candidate is not null && candidate.Element.GetType() == element.GetType())
            {
                node = candidate;
                node.Slot = i;
                UpdateNode(node, element);
            }
            else
            {
                if (candidate is not null)
                {
                    (discarded ??= []).Add(candidate);
                }
                node = Mount(element, parent, i);
            }
            next.Add(node);
        }

        parent.Children = next;
        foreach (var node in discarded ?? [])
        {
            Unmount(node);
        }
        foreach (var node in keyed?.Values ?? (IEnumerable<ElementNode>)[])
        {
            Unmount(node);
        }
        foreach (var node in positional?.Values ?? (IEnumerable<ElementNode>)[])
        {
            Unmount(node);
        }
    }

    private void Unmount(ElementNode node)
    {
        foreach (var child in node.Children)
        {
            Unmount(child);
        }
        node.Children = [];
        node.Mounted = false;
        node.Dirty = false;
        _dirty.Remove(node);
        node.Context?.Release();
        if (node.RenderNode is { } renderNode)
        {
            if (ReferenceEquals(_focused, renderNode))
            {
                _focused = null;
            }
            _hovered.Remove(renderNode);
            _pressed?.Remove(renderNode);
            renderNode.Dispose();
            node.RenderNode = null;
        }
    }

    /// <summary>
    /// Sets a host's render children to the render nodes of its element children, in order.
    /// Portals are left out: they are children of the root, after the app, in the order they appeared.
    /// </summary>
    private void SyncRenderChildren(ElementNode host)
    {
        var nodes = new List<RenderNode>();
        foreach (var child in host.Children)
        {
            CollectRenderNodes(child, nodes);
        }
        if (ReferenceEquals(host, _root))
        {
            nodes.AddRange(_portals);
        }
        host.RenderNode!.SetChildren(nodes);

        static void CollectRenderNodes(ElementNode node, List<RenderNode> into)
        {
            if (node.RenderNode is { } renderNode)
            {
                if (renderNode is not PortalRenderNode)
                {
                    into.Add(renderNode);
                }
                return;
            }
            foreach (var child in node.Children)
            {
                CollectRenderNodes(child, into);
            }
        }
    }

    private void Layout(Vector2 size)
    {
        var yoga = RootRenderNode.Yoga;
        if (size != Size)
        {
            YGNodeStyleSetWidth(yoga, size.X);
            YGNodeStyleSetHeight(yoga, size.Y);
            Size = size;
        }
        ResolveHugs();
        foreach (var grid in _grids)
        {
            grid.ApplyCellWidth();
        }
        // Yoga redoes only the subtrees marked dirty by style or text changes since the last layout.
        YGNodeCalculateLayout(yoga, size.X, size.Y, Facebook.Yoga.YGDirection.LTR);
        // A grid's column width follows its laid-out width; when that moved, lay out again (a few
        // times at most, for grids inside grids).
        for (var pass = 0; pass < 4; pass++)
        {
            var changed = false;
            foreach (var grid in _grids)
            {
                changed |= grid.Resolve();
            }
            if (!changed)
            {
                break;
            }
            YGNodeCalculateLayout(yoga, size.X, size.Y, Facebook.Yoga.YGDirection.LTR);
        }
        foreach (var scroller in _scrollers)
        {
            scroller.SyncExtents();
        }
    }

    /// <summary>
    /// Calls <paramref name="tick"/> with each frame's elapsed seconds (from <see cref="Advance"/>)
    /// until the result is disposed: for things that animate on their own, such as a theme
    /// transition. Frames keep coming while any ticker is registered, so dispose it when done. The
    /// UI isn't <see cref="IsIdle"/> while it runs; see the overload for spinners and delays.
    /// </summary>
    public IDisposable AddTicker(Action<double> tick) => AddTicker(tick, TickerKind.Animation);

    /// <summary>
    /// Calls <paramref name="tick"/> with each frame's elapsed seconds until the result is disposed,
    /// as a <paramref name="kind"/> of ticker, which decides whether it keeps the UI from being
    /// <see cref="IsIdle"/>. <paramref name="reason"/> names it in <see cref="BusyReasons"/>.
    /// </summary>
    public IDisposable AddTicker(Action<double> tick, TickerKind kind, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(tick);
        var entry = new TickerEntry(tick, kind, reason ?? kind.ToString().ToLowerInvariant());
        _tickers.Add(entry);
        FrameRequested?.Invoke();
        return new Ticker(() => _tickers.Remove(entry));
    }

    /// <summary>Raised after focus moves (commands that follow focus listen).</summary>
    internal event Action? FocusChanged;

    /// <summary>Whether focus is on <paramref name="node"/>'s content or inside it.</summary>
    internal bool FocusWithin(ElementNode node)
    {
        for (var at = _focused?.Owner; at is not null; at = at.Parent)
        {
            if (ReferenceEquals(at, node))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The commands registered in this UI.</summary>
    public CommandRegistry Commands => _commands ??= new CommandRegistry(this);

    private CommandRegistry? _commands;

    /// <summary>
    /// Runs <paramref name="run"/> when <paramref name="chord"/> is pressed and nothing focused
    /// handles it, wherever focus is (or with nothing focused), until the result is disposed. Of
    /// shortcuts for the same chord, the one with the greatest <paramref name="depth"/> (a
    /// component's depth in the tree: a dialog's over the app's) runs, then the latest added.
    /// </summary>
    public IDisposable AddShortcut(KeyChord chord, Action run, int depth = 0)
    {
        ArgumentNullException.ThrowIfNull(run);
        return AddShortcut(chord, () =>
        {
            run();
            return true;
        }, depth);
    }

    /// <summary>
    /// A shortcut that may decline: <paramref name="tryRun"/> returns false to let the chord go on
    /// to the next shortcut (a command that isn't active where focus is).
    /// </summary>
    internal IDisposable AddShortcut(KeyChord chord, Func<bool> tryRun, int depth)
    {
        var entry = (chord, tryRun, depth);
        var at = _shortcuts.FindLastIndex(s => s.Depth <= depth) + 1;
        _shortcuts.Insert(at, entry);
        return new Ticker(() => _shortcuts.Remove(entry));
    }

    private sealed class Ticker(Action remove) : IDisposable
    {
        private Action? _remove = remove;

        public void Dispose()
        {
            _remove?.Invoke();
            _remove = null;
        }
    }

    internal void AddScroller(ScrollRenderNode scroller) => _scrollers.Add(scroller);

    internal void AddGrid(GridRenderNode grid) => _grids.Add(grid);

    /// <summary>Registers (or, with <paramref name="hugs"/> false, forgets) a node whose style hugs (<see cref="Align.Hug"/>).</summary>
    internal void TrackHug(Facebook.Yoga.Node node, bool hugs)
    {
        if (hugs)
        {
            _hugs.Add(node);
        }
        else
        {
            _hugs.Remove(node);
        }
    }

    // A hugging node takes its parent's alignment, but the start where the parent stretches. Only a
    // change marks the node dirty, so a settled tree costs a look at each hugging node.
    private void ResolveHugs()
    {
        foreach (var node in _hugs)
        {
            if (YGNodeGetOwner(node) is not { } parent)
            {
                continue;
            }
            var align = YGNodeStyleGetAlignItems(parent) == Facebook.Yoga.YGAlign.Stretch ? Facebook.Yoga.YGAlign.FlexStart : Facebook.Yoga.YGAlign.Auto;
            if (YGNodeStyleGetAlignSelf(node) != align)
            {
                YGNodeStyleSetAlignSelf(node, align);
            }
        }
    }

    internal void RemoveGrid(GridRenderNode grid) => _grids.Remove(grid);

    internal void AddPortal(PortalRenderNode portal)
    {
        _portals.Add(portal);
        _portalsChanged = true;
    }

    internal void RemovePortal(PortalRenderNode portal)
    {
        _portals.Remove(portal);
        _portalsChanged = true;
    }

    internal void RemoveScroller(ScrollRenderNode scroller)
    {
        _scrollers.Remove(scroller);
        _animating.Remove(scroller);
    }

    internal void StartAnimating(ScrollRenderNode scroller)
    {
        if (_animating.Add(scroller))
        {
            FrameRequested?.Invoke();
        }
    }

    // ------------------------------------------------------------------ input

    /// <summary>The pointer moved to <paramref name="position"/> (root coordinates).</summary>
    public void PointerMove(Vector2 position, KeyModifiers modifiers = KeyModifiers.None)
    {
        Received(UIInputType.PointerMove, position, modifiers: modifiers);
        var path = HitPath(position);
        UpdateHover(path, position, modifiers);
        var args = new PointerEventArgs(position, PointerButton.Left, modifiers);
        Dispatch(_pressed ?? path, args, box => null, box => box.OnPointerMove, (node, e) => node.OnPointerMove(e));
    }

    /// <summary>A pointer button was pressed at <paramref name="position"/>.</summary>
    public void PointerDown(Vector2 position, PointerButton button = PointerButton.Left, KeyModifiers modifiers = KeyModifiers.None)
    {
        Received(UIInputType.PointerDown, position, button, modifiers);
        UsingKeyboard = false;
        var path = HitPath(position);
        UpdateHover(path, position, modifiers);
        // The press is captured: its moves and release go to where it began.
        _pressed = path;

        if (_pointerDownObservers.Count > 0)
        {
            var observation = new PointerDownObservation(position, button, path);
            foreach (var observer in _pointerDownObservers.ToArray())
            {
                observer(observation);
            }
        }

        var focusable = path.FindLast(node => node is BoxRenderNode { Element.Focusable: true });
        SetFocus(focusable, visible: false);

        var args = new PointerEventArgs(position, button, modifiers, CountClick(position, button));
        Dispatch(path, args, box => box.OnPointerDownCapture, box => box.OnPointerDown, (node, e) => node.OnPointerDown(e));
    }

    /// <summary>A pointer button was released at <paramref name="position"/>.</summary>
    public void PointerUp(Vector2 position, PointerButton button = PointerButton.Left, KeyModifiers modifiers = KeyModifiers.None)
    {
        Received(UIInputType.PointerUp, position, button, modifiers);
        var pressed = _pressed;
        _pressed = null;
        var path = HitPath(position);
        var args = new PointerEventArgs(position, button, modifiers, _lastClick.Count);
        Dispatch(pressed ?? path, args, box => null, box => box.OnPointerUp, (node, e) => node.OnPointerUp(e));

        // A click goes to the deepest box both the press and the release were over.
        if (pressed is not null)
        {
            var common = new List<RenderNode>();
            for (var i = 0; i < Math.Min(pressed.Count, path.Count) && ReferenceEquals(pressed[i], path[i]); i++)
            {
                common.Add(path[i]);
            }
            if (common.Count > 0)
            {
                Dispatch(common, new PointerEventArgs(position, button, modifiers, _lastClick.Count), box => null, box => box.OnClick);
            }
        }
        UpdateHover(path, position, modifiers);
    }

    /// <summary>The wheel or trackpad scrolled by <paramref name="delta"/> pixels over <paramref name="position"/>.</summary>
    public void Wheel(Vector2 position, Vector2 delta, KeyModifiers modifiers = KeyModifiers.None)
    {
        if (InputReceived is { } received)
        {
            received(new UIInputEvent(UIInputType.Wheel) { Position = position, Delta = delta, Modifiers = modifiers, TargetId = TopmostId(position) });
        }
        var args = new PointerEventArgs(position, PointerButton.Left, modifiers, wheelDelta: delta);
        Dispatch(HitPath(position), args, box => null, box => box.OnWheel, (node, e) => node.OnWheel(e));
    }

    /// <summary>
    /// A key was pressed. It goes to the focused box and its ancestors first; left unhandled, it
    /// runs a matching shortcut (the deepest in the tree first, then the latest added), and Tab and
    /// Shift+Tab move focus.
    /// </summary>
    public void KeyDown(KeyCode key, KeyModifiers modifiers = KeyModifiers.None, bool isRepeat = false)
    {
        InputReceived?.Invoke(new UIInputEvent(UIInputType.KeyDown) { Key = key, Modifiers = modifiers, IsRepeat = isRepeat, TargetId = FocusedId });
        UsingKeyboard = true;
        var args = new KeyEventArgs(key, modifiers, isRepeat);
        Dispatch(FocusPath(), args, box => box.OnKeyDownCapture, box => box.OnKeyDown);
        for (var i = _shortcuts.Count - 1; i >= 0 && !args.Handled; i--)
        {
            if (_shortcuts[i].Chord.Matches(key, modifiers) && _shortcuts[i].Run())
            {
                args.Handled = true;
            }
        }
        if (!args.Handled && key == KeyCode.Tab)
        {
            MoveFocus(forward: (modifiers & KeyModifiers.Shift) == 0);
        }
    }

    /// <summary>A key was released.</summary>
    public void KeyUp(KeyCode key, KeyModifiers modifiers = KeyModifiers.None)
    {
        InputReceived?.Invoke(new UIInputEvent(UIInputType.KeyUp) { Key = key, Modifiers = modifiers, TargetId = FocusedId });
        Dispatch(FocusPath(), new KeyEventArgs(key, modifiers, false), box => null, box => box.OnKeyUp);
    }

    /// <summary>Files were dropped on the window at <paramref name="position"/> (the box under it and its ancestors hear it).</summary>
    public void DropFiles(Vector2 position, IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (InputReceived is { } received)
        {
            received(new UIInputEvent(UIInputType.FileDrop) { Position = position, Paths = paths, TargetId = TopmostId(position) });
        }
        Dispatch(HitPath(position), new FileDropEventArgs(position, paths), box => null, box => box.OnFileDrop);
    }

    /// <summary>Text was typed.</summary>
    public void TextInput(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ReportText(text);
        Dispatch(FocusPath(), new TextInputEventArgs(text), box => null, box => box.OnTextInput);
    }

    /// <summary>
    /// Tells <see cref="InputReceived"/> of text typed: what <see cref="TextInput"/> is given, and what the
    /// platform commits straight to the focused text input client.
    /// </summary>
    internal void ReportText(string text) =>
        InputReceived?.Invoke(new UIInputEvent(UIInputType.Text) { Text = text, TargetId = FocusedId });

    private void Received(UIInputType type, Vector2 position, PointerButton button = PointerButton.Left, KeyModifiers modifiers = KeyModifiers.None)
    {
        if (InputReceived is { } received)
        {
            received(new UIInputEvent(type) { Position = position, Button = button, Modifiers = modifiers, TargetId = TopmostId(position) });
        }
    }

    private int TopmostId(Vector2 position)
    {
        if (!_mounted)
        {
            return 0;
        }
        var hits = new List<RenderNode>();
        RootRenderNode.HitTest(position, hits);
        return hits.Count == 0 ? 0 : hits[0].Id;
    }

    /// <summary>
    /// Moves focus to the next (or previous) focusable box in tree order, wrapping around: within
    /// the most recent focus trap, if one is showing.
    /// </summary>
    public void MoveFocus(bool forward)
    {
        var order = new List<RenderNode>();
        CollectTabStops(_focusTraps.Count > 0 ? _focusTraps[^1] : RootRenderNode, order);
        if (order.Count == 0)
        {
            return;
        }
        var index = _focused is null ? -1 : order.IndexOf(_focused);
        var next = index < 0
            ? (forward ? 0 : order.Count - 1)
            : (index + (forward ? 1 : order.Count - 1)) % order.Count;
        SetFocus(order[next], visible: true);

    }

    private static void CollectTabStops(RenderNode node, List<RenderNode> into)
    {
        if (node is BoxRenderNode { Element: { Focusable: true, TabIndex: >= 0 } })
        {
            into.Add(node);
        }
        foreach (var child in node.Children)
        {
            CollectTabStops(child, into);
        }
    }

    /// <summary>Takes focus away from whatever has it.</summary>
    public void ClearFocus() => SetFocus(null, visible: false);

    /// <summary>
    /// Remembers what has focus now, so it can be given back: a dialog saves focus when it opens
    /// and restores it when it closes.
    /// </summary>
    public FocusSnapshot SaveFocus() => new(this, _focused, _focusVisible);

    internal void RestoreFocus(RenderNode? node, bool visible)
    {
        if (node is null || node.Owner.Mounted)
        {
            SetFocus(node, visible);
        }
    }

    /// <summary>
    /// Whether the last input was a key rather than a press: focus moved by code then shows its
    /// ring only if the user is on the keyboard, as a browser's <c>:focus-visible</c> does.
    /// </summary>
    public bool UsingKeyboard { get; private set; }

    /// <summary>
    /// Focuses the first focusable box inside <paramref name="scope"/>'s box (or the box itself);
    /// false if there is none. Its ring shows if <paramref name="visible"/> says so, or, left
    /// null, if the user is on the keyboard (<see cref="UsingKeyboard"/>).
    /// </summary>
    public bool FocusFirst(ElementRef scope, bool? visible = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Node is not { } node)
        {
            return false;
        }
        var order = new List<RenderNode>();
        CollectTabStops(node, order);
        if (order.Count == 0)
        {
            return false;
        }
        SetFocus(order[0], visible ?? UsingKeyboard);
        return true;
    }

    /// <summary>
    /// Calls <paramref name="observer"/> for every pointer press anywhere, before it's dispatched,
    /// until the result is disposed: how a popover notices a press outside it.
    /// </summary>
    public IDisposable ObservePointerDown(Action<PointerDownObservation> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        _pointerDownObservers.Add(observer);
        return new Ticker(() => _pointerDownObservers.Remove(observer));
    }

    internal void AddFocusTrap(BoxRenderNode trap) => _focusTraps.Add(trap);

    internal void RemoveFocusTrap(BoxRenderNode trap) => _focusTraps.Remove(trap);

    internal void Focus(RenderNode node, bool visible) => SetFocus(node, visible);

    /// <summary>
    /// The accessibility tree as of the last layout: boxes with <see cref="Box.Semantics"/> or
    /// focus, and text, in the order they are drawn; other boxes pass their children up.
    /// </summary>
    public SemanticsNode GetSemantics()
    {
        var children = new List<SemanticsNode>();
        if (_mounted)
        {
            foreach (var child in RootRenderNode.Children)
            {
                CollectSemantics(child, children);
            }
        }
        return new SemanticsNode(0, new Semantics { Role = SemanticsRole.Group }, null,
            new System.Drawing.RectangleF(0, 0, Size.X, Size.Y), false, false, children);
    }

    /// <summary>
    /// Presses the node <paramref name="id"/> names (a <see cref="SemanticsNode.Id"/>), as
    /// assistive technology does for "press": it takes focus if it can, and its box and ancestors
    /// hear a click at its centre, whatever may be drawn over it. False if there's no such node.
    /// </summary>
    public bool Press(int id)
    {
        if (Find(id) is not { } node)
        {
            return false;
        }
        if (node is BoxRenderNode { Element.Focusable: true })
        {
            SetFocus(node, visible: true);
        }
        var centre = node.AbsolutePosition + node.Size / 2;
        Dispatch(EventPath(node), new PointerEventArgs(centre, PointerButton.Left, KeyModifiers.None, 1), box => null, box => box.OnClick);
        FrameRequested?.Invoke();
        return true;
    }

    /// <summary>Gives the node <paramref name="id"/> names keyboard focus, as assistive technology moving focus does. False if it can't take it.</summary>
    public bool FocusNode(int id)
    {
        if (Find(id) is not BoxRenderNode { Element.Focusable: true } node)
        {
            return false;
        }
        SetFocus(node, visible: true);
        FrameRequested?.Invoke();
        return true;
    }

    /// <summary>The <see cref="SemanticsNode.Id"/> of the focused node, or 0.</summary>
    public int FocusedId => _focused?.Id ?? 0;

    /// <summary>The root of the laid-out tree, or null before the first <see cref="Update"/>.</summary>
    public UINode? RootNode => _mounted ? new UINode(this, RootRenderNode) : null;

    /// <summary>The laid-out node <paramref name="id"/> names (a <see cref="SemanticsNode.Id"/>), or null.</summary>
    public UINode? FindNode(int id) => _mounted && Find(id) is { } node ? new UINode(this, node) : null;

    /// <summary>
    /// The nodes drawn under a point (root coordinates), topmost first, each followed by the node it's
    /// drawn in, down to the root: what a press there lands on. Empty if nothing is there.
    /// </summary>
    public IReadOnlyList<UINode> HitTest(Vector2 position)
    {
        if (!_mounted)
        {
            return [];
        }
        var hits = new List<RenderNode>();
        RootRenderNode.HitTest(position, hits);
        return [.. hits.Select(node => new UINode(this, node))];
    }

    /// <summary>
    /// Scrolls every scroll area the node <paramref name="id"/> names is in, innermost first, as little
    /// as brings it into view. False if there's no such node.
    /// </summary>
    public bool ScrollIntoView(int id, bool animated = false)
    {
        if (Find(id) is not { } node)
        {
            return false;
        }
        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is not ScrollRenderNode scroller)
            {
                continue;
            }
            // Bounds are worked out again for each area: scrolling an inner one moves nothing outside it.
            var bounds = node.RootBounds();
            var area = scroller.RootBounds();
            var controller = scroller.Controller;
            var offset = controller.Offset;
            if (controller.CanScrollVertical)
            {
                controller.ScrollIntoView(bounds.Y - area.Y + offset.Y, Math.Min(bounds.Height, controller.ViewportSize.Y), vertical: true, animated);
            }
            if (controller.CanScrollHorizontal)
            {
                controller.ScrollIntoView(bounds.X - area.X + offset.X, Math.Min(bounds.Width, controller.ViewportSize.X), vertical: false, animated);
            }
        }
        FrameRequested?.Invoke();
        return true;
    }

    /// <summary>
    /// Scrolls the scroll area <paramref name="id"/> names to <paramref name="offset"/> (from its content's
    /// start), within its range. False if it names no scroll area.
    /// </summary>
    public bool ScrollTo(int id, Vector2 offset, bool animated = false)
    {
        if (Find(id) is not ScrollRenderNode scroller)
        {
            return false;
        }
        scroller.Controller.ScrollTo(Vector2.Clamp(offset, Vector2.Zero, Vector2.Max(scroller.Controller.MaxOffset, Vector2.Zero)), animated);
        FrameRequested?.Invoke();
        return true;
    }

    /// <summary>
    /// A node's test ID: its semantics' <see cref="Semantics.TestId"/>, else the outermost
    /// <see cref="Element.TestId"/> among the element that made it and the components above that
    /// build nothing but it, else the outermost of their generated root names (<see cref="Element.DefaultTestId"/>).
    /// </summary>
    internal static string? TestIdOf(RenderNode node)
    {
        var ids = TestIdsOf(node);
        return ids.Count == 0 ? null : ids[0];
    }

    /// <summary>
    /// Every test ID a node answers to, the one it's shown by first: its semantics'
    /// <see cref="Semantics.TestId"/>, then the explicit <see cref="Element.TestId"/>s of the element that
    /// made it and the components above that build nothing else in the flow (outermost first), then their
    /// generated root names (<see cref="Element.DefaultTestId"/>, outermost first). A caller's ID and a
    /// component's own part ID on one node both find it.
    /// </summary>
    internal static IReadOnlyList<string> TestIdsOf(RenderNode node)
    {
        var declared = node switch
        {
            BoxRenderNode box => box.Element.Semantics?.TestId,
            ScrollRenderNode scroll => scroll.Element.Semantics?.TestId,
            CanvasRenderNode canvas => canvas.Element.Semantics?.TestId,
            _ => null,
        };
        var ids = new List<string>();
        if (declared is not null)
        {
            ids.Add(declared);
        }
        if (node.Owner is not { } owner)
        {
            return ids;
        }
        var explicitIds = new List<string>();
        var generated = new List<string>();
        Take(owner);
        for (ElementNode from = owner, above = owner.Parent!; above is not null && Continues(above, from); from = above, above = above.Parent!)
        {
            Take(above);
        }
        foreach (var id in explicitIds.Concat(generated))
        {
            if (!ids.Contains(id))
            {
                ids.Add(id);
            }
        }
        return ids;

        void Take(ElementNode at)
        {
            if (at.Element.TestId is { } id)
            {
                explicitIds.Insert(0, id);
            }
            if (at.Element.DefaultTestId is { } name)
            {
                generated.Insert(0, name);
            }
        }
    }

    // Whether a component above builds nothing but what's below it: its only child, or its only child in
    // the flow when the others just put up a popup (a portal, or nothing while it's closed).
    private static bool Continues(ElementNode above, ElementNode from) =>
        above.RenderNode is null
        && (above.Children.Count == 1 || InFlow(from, 2) == 1 && InFlow(above, 2) == 1);

    // How many render nodes a node puts in the layout's flow (portals aren't), counting no further than limit.
    private static int InFlow(ElementNode node, int limit)
    {
        if (node.RenderNode is { } renderNode)
        {
            return renderNode is PortalRenderNode ? 0 : 1;
        }
        var count = 0;
        foreach (var child in node.Children)
        {
            count += InFlow(child, limit - count);
            if (count >= limit)
            {
                break;
            }
        }
        return count;
    }

    private RenderNode? Find(int id)
    {
        RenderNode? Search(RenderNode node)
        {
            if (node.Id == id)
            {
                return node;
            }
            foreach (var child in node.Children)
            {
                if (Search(child) is { } found)
                {
                    return found;
                }
            }
            return null;
        }
        // Portals are the root's children too, so their contents are found as well.
        return Search(RootRenderNode);
    }

    private void CollectSemantics(RenderNode node, List<SemanticsNode> into)
    {
        var children = new List<SemanticsNode>();
        foreach (var child in node.Children)
        {
            CollectSemantics(child, children);
        }
        var bounds = node.RootBounds();
        var testId = TestIdOf(node);
        switch (node)
        {
            case TextRenderNode { Element.IsDecorative: true }:
                break;
            case TextRenderNode text:
                var textSemantics = text.Element.HeadingLevel > 0
                    ? new Semantics { Role = SemanticsRole.Heading, HeadingLevel = text.Element.HeadingLevel }
                    : new Semantics { Role = SemanticsRole.Text };
                into.Add(new SemanticsNode(node.Id, textSemantics, text.Element.AttributedText.Text, bounds, false, false, [], testId));
                break;
            case ImageRenderNode { Element.AltText: { } alt }:
                into.Add(new SemanticsNode(node.Id, new Semantics { Role = SemanticsRole.Image, Label = alt }, alt, bounds, false, false, [], testId));
                break;
            case CanvasRenderNode { Element.Semantics: { } canvasSemantics }:
                into.Add(new SemanticsNode(node.Id, canvasSemantics, canvasSemantics.Label, bounds, false, false, children, testId));
                break;
            case ScrollRenderNode scroll:
                var scrollSemantics = scroll.Element.Semantics ?? new Semantics { Role = SemanticsRole.ScrollArea };
                into.Add(new SemanticsNode(node.Id, scrollSemantics, scrollSemantics.Label, bounds, false, false, children, testId));
                break;
            case BoxRenderNode { Element: var box } when box.Semantics is not null || box.Focusable || testId is not null:
                // A box named only for tests has no role: assistive technology passes over it.
                var semantics = box.Semantics ?? new Semantics { Role = box.Focusable ? SemanticsRole.Group : SemanticsRole.None };
                // A control named by its text (a button's label) takes it as its own name.
                var label = semantics.Label ?? (box.Semantics is null && !box.Focusable ? null : JoinText(children));
                if (semantics.Label is null && semantics.Role is not (SemanticsRole.Group or SemanticsRole.List or SemanticsRole.None))
                {
                    children.RemoveAll(c => c.Role == SemanticsRole.Text);
                }
                into.Add(new SemanticsNode(node.Id, semantics, label, bounds, box.Focusable, ReferenceEquals(node, _focused), children, testId));
                break;
            default:
                if (testId is not null)
                {
                    // A named node that's no box (a portal's layer, a grid): in the tree as a scope for its parts.
                    into.Add(new SemanticsNode(node.Id, new Semantics { Role = SemanticsRole.None }, null, bounds, false, false, children, testId));
                }
                else
                {
                    into.AddRange(children);
                }
                break;
        }

        static string? JoinText(List<SemanticsNode> nodes)
        {
            var texts = nodes.FindAll(n => n.Role == SemanticsRole.Text).ConvertAll(n => n.Label);
            return texts.Count == 0 ? null : string.Join(" ", texts);
        }
    }

    private void SetFocus(RenderNode? node, bool visible)
    {
        if (ReferenceEquals(node, _focused))
        {
            _focusVisible = visible;
            return;
        }
        var old = _focused;
        _focused = node;
        _focusVisible = visible;
        if (old is BoxRenderNode { Element.OnBlur: { } blur })
        {
            blur(new FocusEventArgs(visible));
        }
        if (node is BoxRenderNode { Element.OnFocus: { } focus })
        {
            focus(new FocusEventArgs(visible));
        }
        FocusChanged?.Invoke();
    }

    private int CountClick(Vector2 position, PointerButton button)
    {
        var now = Clock();
        var last = _lastClick;
        var count = last.Button == button && now - last.Time <= DoubleClickSeconds
            && Vector2.Distance(last.Position, position) <= DoubleClickDistance ? last.Count + 1 : 1;
        _lastClick = (now, position, button, count);
        return count;
    }

    /// <summary>
    /// The path an event under a point travels: from the root to the topmost render node there,
    /// through its element ancestors. That's its visual ancestors, except that content in a
    /// portal bubbles to the elements that rendered the portal, not to the root it's drawn in.
    /// </summary>
    internal List<RenderNode> HitPath(Vector2 position)
    {
        if (!_mounted)
        {
            return [];
        }
        var hits = new List<RenderNode>();
        RootRenderNode.HitTest(position, hits);
        return hits.Count == 0 ? [] : EventPath(hits[0]);
    }

    private List<RenderNode> FocusPath() => _focused is null ? [] : EventPath(_focused);

    private static List<RenderNode> EventPath(RenderNode target)
    {
        var path = new List<RenderNode>();
        for (var node = target.Owner; node is not null; node = node.Parent)
        {
            if (node.RenderNode is { } renderNode)
            {
                path.Add(renderNode);
            }
        }
        path.Reverse();
        return path;
    }

    private void UpdateHover(List<RenderNode> path, Vector2 position, KeyModifiers modifiers)
    {
        var old = _hovered;
        _hovered = path;
        // Leave, deepest first, for what the pointer is no longer over; enter, outermost first, for what it now is.
        for (var i = old.Count - 1; i >= 0; i--)
        {
            if (!path.Contains(old[i]) && old[i] is BoxRenderNode { Element.OnPointerLeave: { } leave } box)
            {
                leave(new PointerEventArgs(position, PointerButton.Left, modifiers) { LocalPosition = box.ToLocal(position) });
            }
        }
        foreach (var node in path)
        {
            if (!old.Contains(node) && node is BoxRenderNode { Element.OnPointerEnter: { } enter } box)
            {
                enter(new PointerEventArgs(position, PointerButton.Left, modifiers) { LocalPosition = box.ToLocal(position) });
            }
        }
        RefreshCursor();
    }

    /// <summary>Works out <see cref="Cursor"/> from the pressed or hovered path, deepest box first.</summary>
    private void RefreshCursor()
    {
        var path = _pressed ?? _hovered;
        var cursor = CursorShape.Arrow;
        for (var i = path.Count - 1; i >= 0; i--)
        {
            if (path[i] is BoxRenderNode { Element.Cursor: { } shape })
            {
                cursor = shape;
                break;
            }
        }
        if (cursor != Cursor)
        {
            Cursor = cursor;
            CursorChanged?.Invoke(cursor);
        }
    }

    /// <summary>Sends an event down a path (capture handlers) and back up (the others) until handled.</summary>
    private static void Dispatch<TArgs>(List<RenderNode> path, TArgs args,
        Func<Box, Action<TArgs>?> capture, Func<Box, Action<TArgs>?> bubble,
        Action<RenderNode, TArgs>? defaultAction = null) where TArgs : UIEventArgs
    {
        for (var i = 0; i < path.Count && !args.Handled; i++)
        {
            Invoke(path[i], capture);
        }
        for (var i = path.Count - 1; i >= 0 && !args.Handled; i--)
        {
            Invoke(path[i], bubble);
            // What the node itself does with an event its handlers left alone (a scroll area scrolls).
            if (!args.Handled && defaultAction is not null)
            {
                defaultAction(path[i], args);
            }
        }

        void Invoke(RenderNode node, Func<Box, Action<TArgs>?> select)
        {
            if (node is BoxRenderNode box && select(box.Element) is { } handler)
            {
                if (args is PointerEventArgs pointer)
                {
                    pointer.LocalPosition = node.ToLocal(pointer.Position);
                }
                handler(args);
            }
        }
    }

    /// <summary>Unmounts everything: effects clean up and render nodes are freed.</summary>
    public void Dispose()
    {
        if (_mounted)
        {
            Unmount(_root);
            _mounted = false;
        }
    }
}
