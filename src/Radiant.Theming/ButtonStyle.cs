namespace Radiant.Theming;

/// <summary>How labelled buttons are drawn: their size, shape, label and each variant's look.</summary>
public sealed record ButtonStyle
{
    /// <summary>The height at standard density, in pixels.</summary>
    public float Height { get; init; } = 40f;

    /// <summary>The padding at each end, in pixels.</summary>
    public float Padding { get; init; } = 24f;

    /// <summary>A text button's padding at each end, in pixels.</summary>
    public float TextPadding { get; init; } = 12f;

    /// <summary>The leading icon's size, in pixels.</summary>
    public float IconSize { get; init; } = 18f;

    /// <summary>The corners.</summary>
    public CornerShapeRole Shape { get; init; } = CornerShapeRole.Control;

    /// <summary>The label's text style.</summary>
    public TextType Label { get; init; } = TextType.LabelLarge;

    /// <summary>A floating action button's corners.</summary>
    public CornerShapeRole FabShape { get; init; } = CornerShapeRole.Large;

    /// <summary>How raised a floating action button is.</summary>
    public ElevationLevel FabElevation { get; init; } = ElevationLevel.Level3;

    /// <summary>The main action.</summary>
    public SurfaceLook Filled { get; init; } = new() { Surface = SurfaceName.Primary };

    /// <summary>Important, but not the main action.</summary>
    public SurfaceLook Tonal { get; init; } = new() { Surface = SurfaceName.Secondary, SurfaceContainer = true };

    /// <summary>Secondary actions.</summary>
    public SurfaceLook Outlined { get; init; } = new() { Content = SurfaceName.Primary, Outline = true };

    /// <summary>The least prominent actions.</summary>
    public SurfaceLook Text { get; init; } = new() { Content = SurfaceName.Primary };

    /// <summary>A toggle button while it's on.</summary>
    public SurfaceLook ToggleOn { get; init; } = new() { Surface = SurfaceName.Secondary, SurfaceContainer = true };

    /// <summary>Raised, for busy backgrounds.</summary>
    public SurfaceLook Elevated { get; init; } = new()
    {
        Surface = SurfaceName.SurfaceContainerLow,
        Content = SurfaceName.Primary,
        Elevation = ElevationLevel.Level1,
    };
}
