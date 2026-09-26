using System;

namespace Radiant.Theming;

/// <summary>Which of the user's appearance settings a theme follows (all of them by default).</summary>
public sealed record AppearanceFollowing
{
    /// <summary>Dark mode sets <see cref="ThemeColors.IsDark"/>.</summary>
    public bool DarkMode { get; init; } = true;

    /// <summary>The accent colour becomes the <see cref="ThemeColors.Seed"/>.</summary>
    public bool Accent { get; init; } = true;

    /// <summary>"Increase contrast" sets <see cref="ThemeColors.ContrastLevel"/> to 1, the highest.</summary>
    public bool Contrast { get; init; } = true;

    /// <summary>"Reduce motion" sets <see cref="MotionScheme.Reduced"/>.</summary>
    public bool Motion { get; init; } = true;

    /// <summary>How long a change the user makes takes to animate in; the theme's medium duration if null.</summary>
    public TimeSpan? Transition { get; init; }
}
