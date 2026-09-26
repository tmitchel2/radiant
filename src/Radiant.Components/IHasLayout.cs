using Radiant.Layout;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>A component's size and placement, overriding its own defaults.</summary>
[StyleFacet]
public interface IHasLayout
{
    /// <summary>The layout.</summary>
    LayoutStyle? Layout { get; init; }
}
