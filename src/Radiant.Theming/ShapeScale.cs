namespace Radiant.Theming;

/// <summary>The corner radius, in pixels, of each <see cref="CornerShapeRole"/>. Change it to reshape the whole app.</summary>
public sealed record ShapeScale
{
    /// <summary>Extra small.</summary>
    public float ExtraSmall { get; init; } = 4f;

    /// <summary>Small.</summary>
    public float Small { get; init; } = 8f;

    /// <summary>Medium.</summary>
    public float Medium { get; init; } = 12f;

    /// <summary>Large.</summary>
    public float Large { get; init; } = 16f;

    /// <summary>Large increased.</summary>
    public float LargeIncreased { get; init; } = 20f;

    /// <summary>Semi-large.</summary>
    public float SemiLarge { get; init; } = 24f;

    /// <summary>Extra large.</summary>
    public float ExtraLarge { get; init; } = 28f;

    /// <summary>Extra large increased.</summary>
    public float ExtraLargeIncreased { get; init; } = 32f;

    /// <summary>Extra extra large.</summary>
    public float ExtraExtraLarge { get; init; } = 48f;

    /// <summary>A scale with every radius multiplied by <paramref name="factor"/>: 0 squares everything off.</summary>
    public ShapeScale Scaled(float factor) => new()
    {
        ExtraSmall = ExtraSmall * factor,
        Small = Small * factor,
        Medium = Medium * factor,
        Large = Large * factor,
        LargeIncreased = LargeIncreased * factor,
        SemiLarge = SemiLarge * factor,
        ExtraLarge = ExtraLarge * factor,
        ExtraLargeIncreased = ExtraLargeIncreased * factor,
        ExtraExtraLarge = ExtraExtraLarge * factor,
    };

    /// <summary>
    /// A role's radius. <see cref="CornerShapeRole.Full"/> is effectively infinite: the renderer
    /// clamps a radius to half the shorter side, which makes a pill or a circle.
    /// </summary>
    public float Radius(CornerShapeRole role) => role switch
    {
        CornerShapeRole.None => 0f,
        CornerShapeRole.ExtraSmall => ExtraSmall,
        CornerShapeRole.Small => Small,
        CornerShapeRole.Medium => Medium,
        CornerShapeRole.Large => Large,
        CornerShapeRole.LargeIncreased => LargeIncreased,
        CornerShapeRole.SemiLarge => SemiLarge,
        CornerShapeRole.ExtraLarge => ExtraLarge,
        CornerShapeRole.ExtraLargeIncreased => ExtraLargeIncreased,
        CornerShapeRole.ExtraExtraLarge => ExtraExtraLarge,
        _ => 100_000f,
    };
}
