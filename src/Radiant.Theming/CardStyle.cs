namespace Radiant.Theming;

/// <summary>How cards are drawn.</summary>
public sealed record CardStyle
{
    /// <summary>The corners.</summary>
    public CornerShapeRole Shape { get; init; } = CornerShapeRole.Medium;

    /// <summary>The padding, in pixels.</summary>
    public float Padding { get; init; } = 16f;

    /// <summary>A raised card.</summary>
    public SurfaceLook Elevated { get; init; } = new() { Surface = SurfaceName.SurfaceContainerLow, Elevation = ElevationLevel.Level1 };

    /// <summary>A filled card.</summary>
    public SurfaceLook Filled { get; init; } = new() { Surface = SurfaceName.SurfaceContainerHighest };

    /// <summary>An outlined card.</summary>
    public SurfaceLook Outlined { get; init; } = new() { Surface = SurfaceName.Surface, Outline = true, OutlineVariant = true };
}
