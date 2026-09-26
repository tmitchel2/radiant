namespace Radiant.Theming;

/// <summary>How chips (assist, filter and input) and tags are drawn.</summary>
public sealed record ChipStyle
{
    /// <summary>A chip's height, in pixels.</summary>
    public float Height { get; init; } = 32f;

    /// <summary>A chip's padding at an end without an icon, in pixels.</summary>
    public float Padding { get; init; } = 16f;

    /// <summary>A chip's corners.</summary>
    public CornerShapeRole Shape { get; init; } = CornerShapeRole.Small;

    /// <summary>A chip's label.</summary>
    public TextType Label { get; init; } = TextType.LabelLarge;

    /// <summary>A chip at rest.</summary>
    public SurfaceLook Rest { get; init; } = new() { Outline = true, OutlineVariant = true };

    /// <summary>A chosen filter chip.</summary>
    public SurfaceLook Chosen { get; init; } = new() { Surface = SurfaceName.Secondary, SurfaceContainer = true };

    /// <summary>A raised chip.</summary>
    public SurfaceLook Elevated { get; init; } = new() { Surface = SurfaceName.SurfaceContainerLow, Elevation = ElevationLevel.Level1 };

    /// <summary>A tag's height, in pixels.</summary>
    public float TagHeight { get; init; } = 24f;

    /// <summary>A tag's corners.</summary>
    public CornerShapeRole TagShape { get; init; } = CornerShapeRole.Small;

    /// <summary>Whether a tag is outlined in its family's colour instead of filled with its container colour.</summary>
    public bool OutlinedTags { get; init; }
}
