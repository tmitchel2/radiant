using Radiant.Graphics2D;

namespace Radiant.Theming;

/// <summary>
/// Every colour role picked by hand, for one appearance (light or dark), instead of worked out
/// from a seed. Set on <see cref="ThemeColors.Light"/> and <see cref="ThemeColors.Dark"/> for a
/// theme with a fixed palette. The roles mean what the seed's scheme's roles mean, so components
/// look the same either way; pick each "on" colour to be readable on its colour.
/// </summary>
public sealed record ColorRoles
{
    /// <summary>The window's background.</summary>
    public required Color Background { get; init; }

    /// <summary>The default surface.</summary>
    public required Color Surface { get; init; }

    /// <summary>A dimmer surface.</summary>
    public required Color SurfaceDim { get; init; }

    /// <summary>A brighter surface.</summary>
    public required Color SurfaceBright { get; init; }

    /// <summary>The lowest-emphasis container.</summary>
    public required Color SurfaceContainerLowest { get; init; }

    /// <summary>A low-emphasis container.</summary>
    public required Color SurfaceContainerLow { get; init; }

    /// <summary>The default container.</summary>
    public required Color SurfaceContainer { get; init; }

    /// <summary>A high-emphasis container.</summary>
    public required Color SurfaceContainerHigh { get; init; }

    /// <summary>The highest-emphasis container: tracks, filled fields.</summary>
    public required Color SurfaceContainerHighest { get; init; }

    /// <summary>The variant surface, behind quieter content.</summary>
    public required Color SurfaceVariant { get; init; }

    /// <summary>Content on the surfaces.</summary>
    public required Color OnSurface { get; init; }

    /// <summary>Quieter content on the surfaces: supporting text, icons.</summary>
    public required Color OnSurfaceVariant { get; init; }

    /// <summary>The inverse surface: tooltips, snackbars.</summary>
    public required Color InverseSurface { get; init; }

    /// <summary>Content on the inverse surface.</summary>
    public required Color InverseOnSurface { get; init; }

    /// <summary>The primary colour for use on the inverse surface.</summary>
    public required Color InversePrimary { get; init; }

    /// <summary>Borders that need to be seen.</summary>
    public required Color Outline { get; init; }

    /// <summary>Quieter borders: dividers, cards.</summary>
    public required Color OutlineVariant { get; init; }

    /// <summary>What dims the app behind a modal.</summary>
    public Color Scrim { get; init; } = Color.Black;

    /// <summary>The colour of elevation shadows.</summary>
    public Color Shadow { get; init; } = Color.Black;

    /// <summary>The main accent.</summary>
    public required ColorFamily Primary { get; init; }

    /// <summary>The quieter accent: tonal buttons, the current navigation item.</summary>
    public required ColorFamily Secondary { get; init; }

    /// <summary>The contrasting accent.</summary>
    public required ColorFamily Tertiary { get; init; }

    /// <summary>Errors and destructive actions.</summary>
    public required ColorFamily Error { get; init; }

    /// <summary>Success.</summary>
    public required ColorFamily Success { get; init; }

    /// <summary>Warnings.</summary>
    public required ColorFamily Warning { get; init; }

    /// <summary>Information.</summary>
    public required ColorFamily Info { get; init; }

    /// <summary>Primary that stays the same in light and dark; primary's container colours if null.</summary>
    public ColorFamily? PrimaryFixed { get; init; }

    /// <summary>Secondary that stays the same in light and dark; secondary's container colours if null.</summary>
    public ColorFamily? SecondaryFixed { get; init; }

    /// <summary>Tertiary that stays the same in light and dark; tertiary's container colours if null.</summary>
    public ColorFamily? TertiaryFixed { get; init; }
}
