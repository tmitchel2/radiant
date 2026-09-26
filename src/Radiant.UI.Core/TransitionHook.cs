using System;
using System.Collections.Generic;
using Radiant.Animation;

namespace Radiant.UI.Core;

/// <summary>
/// A value that moves towards its target over time: each new target starts a transition from the
/// current value, ticked by the root's frames, rebuilding the component each frame until it lands.
/// </summary>
internal sealed class TransitionHook<T>(ElementNode owner, T initial) : IHook
{
    private T _from = initial;
    private IDisposable? _ticker;
    private double _elapsed;

    public T Current { get; private set; } = initial;

    public T Target { get; private set; } = initial;

    public void Retarget(T target, TimeSpan duration, Easing easing, Func<T, T, float, T> lerp)
    {
        if (EqualityComparer<T>.Default.Equals(target, Target))
        {
            return;
        }
        Target = target;
        if (duration <= TimeSpan.Zero)
        {
            Stop();
            Current = target;
            return;
        }
        _from = Current;
        _elapsed = 0;
        _ticker ??= owner.Root.AddTicker(seconds =>
        {
            _elapsed += seconds;
            var progress = (float)Math.Min(1.0, _elapsed / duration.TotalSeconds);
            Current = progress >= 1f ? Target : lerp(_from, Target, easing.Evaluate(progress));
            owner.Root.MarkDirty(owner);
            if (progress >= 1f)
            {
                Stop();
            }
        });
    }

    public void Release() => Stop();

    private void Stop()
    {
        _ticker?.Dispose();
        _ticker = null;
    }
}
