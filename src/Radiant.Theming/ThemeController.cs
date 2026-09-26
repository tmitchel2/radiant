using System;
using Radiant.UI.Core;

namespace Radiant.Theming;

/// <summary>
/// Holds the app's theme and changes it, at once or animated. Components read
/// <see cref="Current"/> (through a <see cref="ThemeProvider"/>) and follow every change.
/// </summary>
public sealed class ThemeController
{
    private readonly Signal<ResolvedTheme> _current;
    private ResolvedTheme? _from;
    private ResolvedTheme? _to;
    private double _elapsed;
    private double _duration;
    private Easing _easing;

    /// <summary>A controller starting with <paramref name="theme"/>.</summary>
    public ThemeController(Theme? theme = null)
    {
        Theme = theme ?? new Theme();
        _current = new Signal<ResolvedTheme>(ResolvedTheme.Resolve(Theme));
    }

    /// <summary>The theme set last (the destination, during a transition).</summary>
    public Theme Theme { get; private set; }

    /// <summary>The theme as it is right now, part way through any transition.</summary>
    public IReadable<ResolvedTheme> Current => _current;

    /// <summary>Whether a transition is running.</summary>
    public bool IsAnimating => _to is not null;

    /// <summary>
    /// Changes the theme. With a <paramref name="transition"/> (and motion not reduced), colours
    /// and shapes move to the new theme over that time along <paramref name="easing"/> (the
    /// theme's standard curve by default); otherwise the change is immediate.
    /// </summary>
    public void Set(Theme theme, TimeSpan transition = default, Easing? easing = null)
    {
        ArgumentNullException.ThrowIfNull(theme);
        Theme = theme;
        var target = ResolvedTheme.Resolve(theme);
        if (transition <= TimeSpan.Zero || theme.Motion.Reduced)
        {
            _to = null;
            _current.Value = target;
            return;
        }
        _from = _current.Value;
        _to = target;
        _elapsed = 0;
        _duration = transition.TotalSeconds;
        _easing = easing ?? theme.Motion.Standard;
    }

    /// <summary>Moves a running transition on by <paramref name="seconds"/>.</summary>
    public void Advance(double seconds)
    {
        if (_to is not { } to || _from is not { } from)
        {
            return;
        }
        _elapsed += seconds;
        var progress = Math.Min(1.0, _elapsed / _duration);
        _current.Value = progress >= 1.0 ? to : ResolvedTheme.Lerp(from, to, _easing.Evaluate((float)progress));
        if (progress >= 1.0)
        {
            _from = _to = null;
        }
    }
}
