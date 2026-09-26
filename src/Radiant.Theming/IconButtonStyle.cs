namespace Radiant.Theming;

/// <summary>How icon buttons are drawn: their size, shape and each variant's look.</summary>
public sealed record IconButtonStyle
{
    /// <summary>The width and height at standard density, in pixels.</summary>
    public float Size { get; init; } = 40f;

    /// <summary>The icon's size, in pixels; the theme's icon size if null.</summary>
    public float? IconSize { get; init; }

    /// <summary>The corners.</summary>
    public CornerShapeRole Shape { get; init; } = CornerShapeRole.Control;

    /// <summary>The most prominent.</summary>
    public SurfaceLook Filled { get; init; } = new() { Surface = SurfaceName.Primary };

    /// <summary>A quieter fill.</summary>
    public SurfaceLook Tonal { get; init; } = new() { Surface = SurfaceName.Secondary, SurfaceContainer = true };

    /// <summary>An outline.</summary>
    public SurfaceLook Outlined { get; init; } = new() { Outline = true, Content = SurfaceName.SurfaceVariant, ContentOn = true };

    /// <summary>Just the icon.</summary>
    public SurfaceLook Standard { get; init; } = new() { Content = SurfaceName.SurfaceVariant, ContentOn = true };
}
