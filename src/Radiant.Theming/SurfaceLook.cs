namespace Radiant.Theming;

/// <summary>
/// How a component variant paints itself, as data a component style holds: the surface and
/// content families, the outline and the elevation. Components turn it into their surface's
/// props, so a style can give a variant a different look without the component knowing.
/// </summary>
public sealed record SurfaceLook
{
    /// <summary>The surface's colour family; null leaves the surface it's on showing through.</summary>
    public SurfaceName? Surface { get; init; }

    /// <summary>Whether the surface takes its family's container colour.</summary>
    public bool SurfaceContainer { get; init; }

    /// <summary>The content's colour family (on the same surface); null for the surface's own content colour.</summary>
    public SurfaceName? Content { get; init; }

    /// <summary>Whether the content takes its family's "on" colour.</summary>
    public bool ContentOn { get; init; }

    /// <summary>The content's legibility, or null for full.</summary>
    public float? ContentLegibility { get; init; }

    /// <summary>Whether a 1 px outline is drawn.</summary>
    public bool Outline { get; init; }

    /// <summary>Whether the outline is the quieter variant.</summary>
    public bool OutlineVariant { get; init; }

    /// <summary>How raised it is.</summary>
    public ElevationLevel? Elevation { get; init; }

    /// <summary>The surface change this look makes.</summary>
    public SurfaceChange ToSurfaceChange() => new()
    {
        Surface = Surface,
        ToggleSurfaceContainer = SurfaceContainer,
        Content = Content,
        ToggleContentOn = ContentOn,
        ContentLegibility = ContentLegibility,
    };
}
