using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>Whether a component has a border, in the theme's outline colour.</summary>
[StyleFacet]
public interface IHasOutline
{
    /// <summary>Whether to draw the outline.</summary>
    bool? ShowOutline { get; init; }

    /// <summary>Its width, 1 px by default.</summary>
    float? OutlineWidth { get; init; }

    /// <summary>Whether to use the quieter outline-variant colour.</summary>
    bool? OutlineVariant { get; init; }

    /// <summary>Whether to draw it in the content colour instead (a primary ring round today's date).</summary>
    bool? OutlineInContentColor { get; init; }

    /// <summary>A colour family to draw it in instead (a featured plan ringed in the accent).</summary>
    Radiant.Theming.SurfaceName? OutlineColor { get; init; }
}
