namespace Radiant.Theming;

/// <summary>How list rows and accordion headers are drawn.</summary>
public sealed record ListStyle
{
    /// <summary>A one-line row's height at standard density, in pixels; two- and three-line rows add <see cref="LineStep"/> each.</summary>
    public float OneLineHeight { get; init; } = 56f;

    /// <summary>How much taller each further line of supporting text makes a row, in pixels.</summary>
    public float LineStep { get; init; } = 16f;

    /// <summary>A row's padding at its start, in pixels (the end has half as much again).</summary>
    public float Padding { get; init; } = 16f;

    /// <summary>A row's headline.</summary>
    public TextType Headline { get; init; } = TextType.BodyLarge;

    /// <summary>A row's supporting text.</summary>
    public TextType Supporting { get; init; } = TextType.BodyMedium;

    /// <summary>A row's corners, seen when it's selected or highlighted.</summary>
    public CornerShapeRole Shape { get; init; } = CornerShapeRole.None;

    /// <summary>A selected row.</summary>
    public SurfaceLook Selected { get; init; } = new() { Surface = SurfaceName.Secondary, SurfaceContainer = true };

    /// <summary>An accordion header's height, in pixels.</summary>
    public float AccordionHeaderHeight { get; init; } = 56f;

    /// <summary>An accordion header's title.</summary>
    public TextType AccordionTitle { get; init; } = TextType.TitleMedium;
}
