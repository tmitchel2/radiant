using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>How raised a component is, as a level on the theme's elevation scale (its shadow).</summary>
[StyleFacet]
public interface IHasElevation
{
    /// <summary>The elevation.</summary>
    ElevationLevel? Elevation { get; init; }
}
