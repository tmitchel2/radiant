using System;

namespace Radiant.UI.Core;

/// <summary>
/// A side effect run after the frame's build and layout, and again when its dependencies change;
/// the cleanup it returns runs before the next run and when the component unmounts.
/// </summary>
internal sealed class EffectHook(ElementNode owner) : IHook
{
    public ElementNode Owner { get; } = owner;

    public bool HasDependencies { get; set; }

    public object? Dependencies { get; set; }

    public Func<Action?>? Pending { get; set; }

    public Action? Cleanup { get; set; }

    public void Run()
    {
        if (Pending is not { } effect)
        {
            return;
        }
        Pending = null;
        Cleanup?.Invoke();
        Cleanup = effect();
    }

    public void Release()
    {
        Pending = null;
        var cleanup = Cleanup;
        Cleanup = null;
        cleanup?.Invoke();
    }
}
