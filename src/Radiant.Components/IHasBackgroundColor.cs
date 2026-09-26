using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// How a component colours itself relative to the surface it sits on (Destash's background
/// colour props): see <see cref="SurfaceChange"/> for what each does.
/// </summary>
[StyleFacet]
public interface IHasBackgroundColor
{
    /// <summary>Paint the surface in this family.</summary>
    SurfaceName? SurfaceColor { get; init; }

    /// <summary>The surface's opacity.</summary>
    float? SurfaceLegibility { get; init; }

    /// <summary>Swap the surface to its "on" colour.</summary>
    bool? SurfaceOnToggle { get; init; }

    /// <summary>Swap the surface to its container colour.</summary>
    bool? SurfaceContainerToggle { get; init; }

    /// <summary>Colour the content in this family.</summary>
    SurfaceName? ContentColor { get; init; }

    /// <summary>The content's opacity.</summary>
    float? ContentLegibility { get; init; }

    /// <summary>Swap the content to its "on" colour.</summary>
    bool? ContentOnToggle { get; init; }

    /// <summary>Swap the content to its container colour.</summary>
    bool? ContentContainerToggle { get; init; }

    /// <summary>Colour focused content in this family.</summary>
    SurfaceName? ContentFocusedColor { get; init; }

    /// <summary>Whether to draw the surface's colour (null: when a surface family or toggle is set).</summary>
    bool? ShowSurface { get; init; }

    /// <summary>Show the error state.</summary>
    bool? ShowError { get; init; }

    /// <summary>Show the disabled state (and, on pressable components, stop responding).</summary>
    bool? ShowDisabled { get; init; }
}
