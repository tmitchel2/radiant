using System;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>What happens when a component is pressed (clicked, or Enter or Space while focused).</summary>
[StyleFacet]
public interface IHasPressable
{
    /// <summary>The action.</summary>
    Action? OnPress { get; init; }
}
