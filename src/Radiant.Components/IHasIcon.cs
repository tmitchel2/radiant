using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>A component's icon: a Material Symbols name such as "check" or "arrow_back".</summary>
[StyleFacet]
public interface IHasIcon
{
    /// <summary>The icon's name.</summary>
    string? Icon { get; init; }

    /// <summary>Whether to draw the filled form (a selected tab, a set favourite).</summary>
    bool? IconFilled { get; init; }

    /// <summary>The icon's size, 24 px by default.</summary>
    float? IconSize { get; init; }
}
