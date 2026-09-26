namespace Radiant.Theming;

/// <summary>
/// Everything that styles an app, as data: colours, corner shapes, type, elevation, state layers,
/// motion, density. Swap it at runtime through a <see cref="ThemeController"/> and the whole app
/// changes, animating if asked.
/// </summary>
public sealed record Theme
{
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
}
