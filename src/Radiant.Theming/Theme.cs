namespace Radiant.Theming;

/// <summary>
/// Everything that styles an app, as data: colours, corner shapes, type, elevation, state layers,
/// motion, density. Swap it at runtime through a <see cref="ThemeController"/> and the whole app
/// changes, animating if asked.
/// </summary>
public sealed record Theme
{
    /// <summary>What the theme is called, for pickers (see <see cref="ThemePresets"/>); null if it has no name.</summary>
    public string? Name { get; init; }

    /// <summary>Colour.</summary>
    public ThemeColors Colors { get; init; } = new();

    /// <summary>Corner radii.</summary>
    public ShapeScale Shape { get; init; } = new();

    /// <summary>Text styles.</summary>
    public TypeScale Typography { get; init; } = TypeScale.Default;

    /// <summary>Shadows.</summary>
    public ElevationScale Elevation { get; init; } = new();

    /// <summary>State layer and disabled opacities.</summary>
    public StateLayerOpacities StateLayers { get; init; } = new();

    /// <summary>Durations and curves.</summary>
    public MotionScheme Motion { get; init; } = new();

    /// <summary>How tightly packed controls are: 0 standard, negative denser (each step 4 px smaller), positive looser.</summary>
    public int Density { get; init; }

    /// <summary>The spacing unit, in pixels: layouts space things in multiples of it.</summary>
    public float SpacingUnit { get; init; } = 4f;

    /// <summary>How the components are built and drawn: sizes, variants' colours, focus and disabled looks, and structures.</summary>
    public ComponentStyles Components { get; init; } = new();

    /// <summary>
    /// <paramref name="style"/> (a preset, say) with this theme's user settings carried over: light
    /// or dark, the contrast level, the seed (the accent the user or system chose, for themes
    /// that use it) and reduced motion. For switching styles without undoing the user's choices.
    /// </summary>
    public Theme WithStyle(Theme style)
    {
        System.ArgumentNullException.ThrowIfNull(style);
        return style with
        {
            Colors = style.Colors with { IsDark = Colors.IsDark, ContrastLevel = Colors.ContrastLevel, Seed = Colors.Seed },
            Motion = style.Motion with { Reduced = Motion.Reduced },
        };
    }
}
