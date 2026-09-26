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
    private readonly HashSet<ScrollRenderNode> _animating = [];
    private readonly List<PortalRenderNode> _portals = [];
    private readonly List<Func<double, bool>> _tickers = [];
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
            ticker(seconds);
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
    public void Paint(Renderer2D renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        if (_mounted)
        {
            new PaintContext(renderer).Paint(RootRenderNode);
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
                    Render(node, node.Element);
                    SyncRenderChildren(node.NearestHost());
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
        var effects = _effects.ToArray();
        _effects.Clear();
        // Children's effects before their parents', as a parent's effect may rely on its children being ready.
        Array.Sort(effects, (a, b) => b.Owner.Depth.CompareTo(a.Owner.Depth));
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
            case Component component:
                node.Dirty = false;
                var context = node.Context ??= new BuildContext(node);
                context.BeginBuild();
                var child = component.Build(context);
                context.EndBuild();
                Reconcile(node, [child]);
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
    /// transition. Frames keep coming while any ticker is registered, so dispose it when done.
    /// </summary>
    public IDisposable AddTicker(Action<double> tick)
    {
        ArgumentNullException.ThrowIfNull(tick);
        Func<double, bool> entry = seconds =>
        {
            tick(seconds);
            return true;
        };
        _tickers.Add(entry);
        FrameRequested?.Invoke();
        return new Ticker(() => _tickers.Remove(entry));
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
        var path = HitPath(position);
        UpdateHover(path, position, modifiers);
        var args = new PointerEventArgs(position, PointerButton.Left, modifiers);
        Dispatch(_pressed ?? path, args, box => null, box => box.OnPointerMove);
    }

    /// <summary>A pointer button was pressed at <paramref name="position"/>.</summary>
    public void PointerDown(Vector2 position, PointerButton button = PointerButton.Left, KeyModifiers modifiers = KeyModifiers.None)
    {
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
        Dispatch(path, args, box => box.OnPointerDownCapture, box => box.OnPointerDown);
    }

    /// <summary>A pointer button was released at <paramref name="position"/>.</summary>
    public void PointerUp(Vector2 position, PointerButton button = PointerButton.Left, KeyModifiers modifiers = KeyModifiers.None)
    {
        var pressed = _pressed;
        _pressed = null;
        var path = HitPath(position);
        var args = new PointerEventArgs(position, button, modifiers, _lastClick.Count);
        Dispatch(pressed ?? path, args, box => null, box => box.OnPointerUp);

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
        var args = new PointerEventArgs(position, PointerButton.Left, modifiers, wheelDelta: delta);
        Dispatch(HitPath(position), args, box => null, box => box.OnWheel, (node, e) => node.OnWheel(e));
    }

    /// <summary>A key was pressed. Unhandled, Tab and Shift+Tab move focus.</summary>
    public void KeyDown(KeyCode key, KeyModifiers modifiers = KeyModifiers.None, bool isRepeat = false)
    {
        var args = new KeyEventArgs(key, modifiers, isRepeat);
        Dispatch(FocusPath(), args, box => box.OnKeyDownCapture, box => box.OnKeyDown);
        if (!args.Handled && key == KeyCode.Tab)
        {
            MoveFocus(forward: (modifiers & KeyModifiers.Shift) == 0);
        }
    }

    /// <summary>A key was released.</summary>
    public void KeyUp(KeyCode key, KeyModifiers modifiers = KeyModifiers.None) =>
        Dispatch(FocusPath(), new KeyEventArgs(key, modifiers, false), box => null, box => box.OnKeyUp);

    /// <summary>Text was typed.</summary>
    public void TextInput(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Dispatch(FocusPath(), new TextInputEventArgs(text), box => null, box => box.OnTextInput);
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

    /// <summary>Focuses the first focusable box inside <paramref name="scope"/>'s box (or the box itself); false if there is none.</summary>
    public bool FocusFirst(ElementRef scope, bool visible = true)
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
        SetFocus(order[0], visible);
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
        return new SemanticsNode(new Semantics { Role = SemanticsRole.Group }, null,
            new System.Drawing.RectangleF(0, 0, Size.X, Size.Y), false, false, children);
    }

    private void CollectSemantics(RenderNode node, List<SemanticsNode> into)
    {
        var children = new List<SemanticsNode>();
        foreach (var child in node.Children)
        {
            CollectSemantics(child, children);
        }
        var position = node.AbsolutePosition;
        var bounds = new System.Drawing.RectangleF(position.X, position.Y, node.Size.X, node.Size.Y);
        switch (node)
        {
            case TextRenderNode { Element.IsDecorative: true }:
                break;
            case TextRenderNode text:
                into.Add(new SemanticsNode(new Semantics { Role = SemanticsRole.Text }, text.Element.AttributedText.Text, bounds, false, false, []));
                break;
            case ImageRenderNode { Element.AltText: { } alt }:
                into.Add(new SemanticsNode(new Semantics { Role = SemanticsRole.Image, Label = alt }, alt, bounds, false, false, []));
                break;
            case CanvasRenderNode { Element.Semantics: { } canvasSemantics }:
                into.Add(new SemanticsNode(canvasSemantics, canvasSemantics.Label, bounds, false, false, children));
                break;
            case BoxRenderNode { Element: var box } when box.Semantics is not null || box.Focusable:
                var semantics = box.Semantics ?? new Semantics();
                // A control named by its text (a button's label) takes it as its own name.
                var label = semantics.Label ?? JoinText(children);
                if (semantics.Label is null && semantics.Role is not (SemanticsRole.Group or SemanticsRole.List or SemanticsRole.None))
                {
                    children.RemoveAll(c => c.Role == SemanticsRole.Text);
                }
                into.Add(new SemanticsNode(semantics, label, bounds, box.Focusable, ReferenceEquals(node, _focused), children));
                break;
            default:
                into.AddRange(children);
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
