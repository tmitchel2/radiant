using System;
using Radiant.Theming;

namespace Radiant.Gallery.ThemeLab;

/// <summary>
/// An edited theme keeps its preset's name, so the preset it started from is always known: it's
/// what the menus tick, and what Reset goes back to.
/// </summary>
internal static class ThemeEditing
{
    /// <summary>How long a choice in the editor animates for.</summary>
    public static TimeSpan Transition { get; } = TimeSpan.FromMilliseconds(250);

    /// <summary>The preset a theme was made from; Tonal for one with no preset's name.</summary>
    public static Theme BaseOf(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return ThemePresets.Find(theme.Name ?? "") ?? ThemePresets.Tonal;
    }

    /// <summary>
    /// The theme back as its preset, keeping what are the user's settings rather than the theme's
    /// look: light or dark, contrast, the seed colour and reduced motion.
    /// </summary>
    public static Theme Reset(Theme theme) => theme.WithStyle(BaseOf(theme));

    /// <summary>Whether a theme differs from its preset in anything but the user's settings.</summary>
    public static bool IsModified(Theme theme)
    {
        var reset = Reset(theme);
        // Elevation isn't edited here, and compares its levels by reference, so it's left out.
        return theme with { Elevation = reset.Elevation } != reset;
    }
}
