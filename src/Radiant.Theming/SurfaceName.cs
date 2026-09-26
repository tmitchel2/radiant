namespace Radiant.Theming;

/// <summary>
/// A family of colours a surface can be painted in. Each has four roles: the colour, the colour
/// of content on it ("on"), a quieter container colour, and content on the container.
/// </summary>
public enum SurfaceName
{
    /// <summary>The default background.</summary>
    Surface,

    /// <summary>A dimmer surface, for the least important areas.</summary>
    SurfaceDim,

    /// <summary>A brighter surface.</summary>
    SurfaceBright,

    /// <summary>The lowest-emphasis container on a surface.</summary>
    SurfaceContainerLowest,

    /// <summary>A low-emphasis container.</summary>
    SurfaceContainerLow,

    /// <summary>The default container on a surface: cards, sheets.</summary>
    SurfaceContainer,

    /// <summary>A high-emphasis container.</summary>
    SurfaceContainerHigh,

    /// <summary>The highest-emphasis container.</summary>
    SurfaceContainerHighest,

    /// <summary>Surface with its content in the quieter variant colour.</summary>
    SurfaceVariant,

    /// <summary>The inverse of the surface: snackbars and tooltips on a light theme are dark.</summary>
    Inverse,

    /// <summary>The main accent: prominent buttons, active states.</summary>
    Primary,

    /// <summary>Primary that stays the same in light and dark themes.</summary>
    PrimaryFixed,

    /// <summary>A quieter accent: filter chips, tonal buttons.</summary>
    Secondary,

    /// <summary>Secondary that stays the same in light and dark themes.</summary>
    SecondaryFixed,

    /// <summary>A contrasting accent, to balance primary and secondary.</summary>
    Tertiary,

    /// <summary>Tertiary that stays the same in light and dark themes.</summary>
    TertiaryFixed,

    /// <summary>Errors and destructive actions.</summary>
    Error,

    /// <summary>Success, from the theme's success colour harmonised to the seed.</summary>
    Success,

    /// <summary>Warnings, from the theme's warning colour harmonised to the seed.</summary>
    Warning,

    /// <summary>Information, from the theme's info colour harmonised to the seed.</summary>
    Info,
}
