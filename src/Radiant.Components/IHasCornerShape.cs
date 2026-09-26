using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>How rounded a component's corners are, as a step on the theme's shape scale.</summary>
[StyleFacet]
public interface IHasCornerShape
{
    /// <summary>The corner shape.</summary>
    CornerShapeRole? CornerShape { get; init; }
}
