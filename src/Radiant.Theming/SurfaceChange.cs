namespace Radiant.Theming;

/// <summary>
/// How a component changes the surface it sits on, as Destash's background-colour props do:
/// a new colour family, legibility, on/container toggles, and error or disabled states. Applied to
/// the enclosing <see cref="SurfaceState"/> by <see cref="SurfaceState.With"/>.
/// </summary>
public sealed record SurfaceChange
{
    /// <summary>Paint the surface in this family (its content becomes the family's "on" colour).</summary>
    public SurfaceName? Surface { get; init; }

    /// <summary>The surface's opacity.</summary>
    public float? SurfaceLegibility { get; init; }

    /// <summary>Swap the surface and its content between the colour and its "on" colour.</summary>
    public bool ToggleSurfaceOn { get; init; }

    /// <summary>Swap the surface and its content between the colour and the container colour.</summary>
    public bool ToggleSurfaceContainer { get; init; }

    /// <summary>Colour the content in this family (on the same surface).</summary>
    public SurfaceName? Content { get; init; }

    /// <summary>The content's opacity.</summary>
    public float? ContentLegibility { get; init; }

    /// <summary>Swap the content between the colour and its "on" colour.</summary>
    public bool ToggleContentOn { get; init; }

    /// <summary>Swap the content between the colour and the container colour.</summary>
    public bool ToggleContentContainer { get; init; }

    /// <summary>Colour focused content in this family.</summary>
    public SurfaceName? ContentFocused { get; init; }

    /// <summary>Show the error state.</summary>
    public bool ShowError { get; init; }

    /// <summary>Show the disabled state.</summary>
    public bool ShowDisabled { get; init; }
}
