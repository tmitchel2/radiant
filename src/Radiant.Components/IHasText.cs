using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>A component's text and its step on the type scale.</summary>
[StyleFacet]
public interface IHasText
{
    /// <summary>The text.</summary>
    string? Text { get; init; }

    /// <summary>The text's type.</summary>
    TextType? TextType { get; init; }
}
