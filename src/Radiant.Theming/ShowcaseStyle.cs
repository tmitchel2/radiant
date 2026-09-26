namespace Radiant.Theming;

/// <summary>How the page templates' showcase blocks are drawn: heroes, calls to action, feature icons, the featured plan and empty states.</summary>
public sealed record ShowcaseStyle
{
    /// <summary>A hero's panel.</summary>
    public SurfaceLook Hero { get; init; } = new() { Surface = SurfaceName.Primary, SurfaceContainer = true };

    /// <summary>A call to action's panel.</summary>
    public SurfaceLook CallToAction { get; init; } = new() { Surface = SurfaceName.Primary, SurfaceContainer = true };

    /// <summary>The corners of heroes and calls to action.</summary>
    public CornerShapeRole PanelShape { get; init; } = CornerShapeRole.ExtraLarge;

    /// <summary>The tile behind a feature's icon.</summary>
    public SurfaceLook FeatureIcon { get; init; } = new() { Surface = SurfaceName.Tertiary, SurfaceContainer = true };

    /// <summary>The featured plan among pricing tiers.</summary>
    public SurfaceLook FeaturedTier { get; init; } = new() { Surface = SurfaceName.SurfaceContainerHigh, Elevation = ElevationLevel.Level1 };

    /// <summary>The disc or tile behind an empty state's icon.</summary>
    public SurfaceLook EmptyIcon { get; init; } = new() { Surface = SurfaceName.Secondary, SurfaceContainer = true };

    /// <summary>The corners behind an empty state's icon.</summary>
    public CornerShapeRole EmptyIconShape { get; init; } = CornerShapeRole.Full;
}
