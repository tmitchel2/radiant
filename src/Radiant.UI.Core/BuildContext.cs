using System;
using System.Collections.Generic;
using Radiant.Animation;

namespace Radiant.UI.Core;

/// <summary>
/// What a component's <see cref="Component.Build"/> is handed: the hooks through which it keeps
/// state, runs effects, and reads contexts and signals.
/// <para>
/// Hooks are matched to their storage by the order they're called in, so a component must call
/// the same hooks in the same order on every build: never inside a condition or a loop whose
/// length changes. Breaking that throws.
/// </para>
/// </summary>
public sealed class BuildContext
{
    private readonly List<IHook> _hooks = [];
    private int _next;
    private bool _firstBuild = true;

    internal BuildContext(ElementNode node) => Node = node;

    internal ElementNode Node { get; }

    /// <summary>The root this component is mounted in.</summary>
    public UIRoot Root => Node.Root;

    /// <summary>A piece of state, starting at <paramref name="initial"/>; setting it rebuilds the component.</summary>
    public State<T> UseState<T>(T initial) => Value(() => new State<T>(initial, Node));

    /// <summary>A piece of state whose starting value is computed on the first build only.</summary>
    public State<T> UseState<T>(Func<T> initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        return Value(() => new State<T>(initial(), Node));
    }

    /// <summary>A mutable box kept across builds, which doesn't rebuild anything when changed.</summary>
    public Ref<T> UseRef<T>(T initial) => Value(() => new Ref<T>(initial));

    /// <summary>
    /// A value computed by <paramref name="compute"/> on the first build and again only when
    /// <paramref name="dependencies"/> changes (by value: pass a tuple of everything it reads).
    /// </summary>
    public T UseMemo<T>(Func<T> compute, object? dependencies)
    {
        ArgumentNullException.ThrowIfNull(compute);
        var hook = Hook(() => new MemoHook());
        if (_firstBuild || !Equals(hook.Dependencies, dependencies))
        {
            hook.Value = compute();
            hook.Dependencies = dependencies;
        }
        return (T)hook.Value!;
    }

    /// <summary>
    /// Runs <paramref name="run"/> when <paramref name="chord"/> is pressed anywhere in the UI
    /// that doesn't handle it itself, while this component is mounted
    /// (<see cref="UIRoot.AddShortcut(KeyChord, System.Action, int)"/>): a component deeper in the tree takes the chord from
    /// one above it. The latest <paramref name="run"/> is used each time.
    /// </summary>
    public void UseShortcut(KeyChord chord, Action run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var latest = UseRef(run);
        latest.Value = run;
        var (root, depth) = (Node.Root, Node.Depth);
        UseEffect(() => root.AddShortcut(chord, () => latest.Value(), depth).Dispose, chord);
    }

    /// <summary>
    /// Runs <paramref name="effect"/> after this build is laid out, and after every later build.
    /// The action it returns, if any, cleans up before the next run and when the component goes.
    /// </summary>
    public void UseEffect(Func<Action?> effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        var hook = Hook(() => new EffectHook(Node));
        hook.HasDependencies = false;
        hook.Pending = effect;
        Node.Root.QueueEffect(hook);
    }

    /// <summary>
    /// Runs <paramref name="effect"/> after the first build, and again only when
    /// <paramref name="dependencies"/> changes (by value). Pass <c>default(ValueTuple)</c> to run once.
    /// </summary>
    public void UseEffect(Func<Action?> effect, object? dependencies)
    {
        ArgumentNullException.ThrowIfNull(effect);
        var hook = Hook(() => new EffectHook(Node));
        if (_firstBuild || !hook.HasDependencies || !Equals(hook.Dependencies, dependencies))
        {
            hook.HasDependencies = true;
            hook.Dependencies = dependencies;
            hook.Pending = effect;
            Node.Root.QueueEffect(hook);
        }
    }

    /// <summary>
    /// The value of the nearest <see cref="Provider{T}"/> of <paramref name="context"/> above this
    /// component, or its default. The component is rebuilt when that value changes.
    /// </summary>
    public T Use<T>(Context<T> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var hook = Hook(() => new ContextHook(Node));
        for (var node = Node.Parent; node is not null; node = node.Parent)
        {
            if (node.Element is Provider<T> provider && ReferenceEquals(provider.Context, context))
            {
                if (!ReferenceEquals(hook.Provider, node))
                {
                    hook.Release();
                    hook.Provider = node;
                    (node.Consumers ??= []).Add(Node);
                }
                return provider.Value;
            }
        }
        hook.Release();
        return context.DefaultValue;
    }

    /// <summary>The value of <paramref name="signal"/>; the component is rebuilt when it changes.</summary>
    public T Watch<T>(IReadable<T> signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        var hook = Hook(() => new SubscriptionHook());
        if (!ReferenceEquals(hook.Source, signal))
        {
            hook.Release();
            hook.Source = signal;
            var node = Node;
            hook.Subscription = signal.Subscribe(() => node.Root.MarkDirty(node));
        }
        return signal.Value;
    }

    /// <summary>
    /// A value that animates to <paramref name="target"/> over <paramref name="duration"/> whenever
    /// the target changes, mixed by <paramref name="lerp"/> along <paramref name="easing"/>
    /// (Material's standard curve by default). The component is rebuilt each frame while it moves.
    /// It starts at the first target, without animating.
    /// </summary>
    public T UseTransition<T>(T target, TimeSpan duration, Func<T, T, float, T> lerp, Easing? easing = null) =>
        UseTransition(target, target, duration, lerp, easing);

    /// <summary>A value that starts at <paramref name="initial"/> and animates to each new target; see the other overload.</summary>
    public T UseTransition<T>(T target, T initial, TimeSpan duration, Func<T, T, float, T> lerp, Easing? easing = null)
    {
        ArgumentNullException.ThrowIfNull(lerp);
        var node = Node;
        var hook = Hook(() => new TransitionHook<T>(node, initial));
        hook.Retarget(target, duration, easing ?? Easing.Standard, lerp);
        return hook.Current;
    }

    /// <summary>A number that animates to <paramref name="target"/>, from <paramref name="initial"/> if given.</summary>
    public float UseTransition(float target, TimeSpan duration, Easing? easing = null, float? initial = null) =>
        UseTransition(target, initial ?? target, duration, static (a, b, t) => a + (b - a) * t, easing);

    internal void BeginBuild() => _next = 0;

    internal void EndBuild()
    {
        if (!_firstBuild && _next != _hooks.Count)
        {
            throw new InvalidOperationException(
                $"{Node} called {_next} hooks this build but {_hooks.Count} before. Hooks must be called in the same order every build.");
        }
        _firstBuild = false;
    }

    internal void Release()
    {
        // Latest first, as a stack unwinds: an effect's cleanup may still read earlier state.
        for (var i = _hooks.Count - 1; i >= 0; i--)
        {
            _hooks[i].Release();
        }
        _hooks.Clear();
    }

    private TValue Value<TValue>(Func<TValue> create) where TValue : class =>
        Hook(() => new ValueHook(create())).Value as TValue ?? throw OrderChanged();

    private InvalidOperationException OrderChanged() => new(
        $"{Node} called hooks in a different order from its last build. Hooks must be called in the same order every build.");

    private THook Hook<THook>(Func<THook> create) where THook : class, IHook
    {
        if (_next < _hooks.Count)
        {
            return _hooks[_next++] as THook ?? throw OrderChanged();
        }
        if (!_firstBuild)
        {
            throw new InvalidOperationException(
                $"{Node} called more hooks than on its first build. Hooks must be called in the same order every build.");
        }
        var hook = create();
        _hooks.Add(hook);
        _next++;
        return hook;
    }
}
