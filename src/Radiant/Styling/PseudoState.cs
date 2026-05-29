using System;

namespace Radiant.Styling;

/// <summary>
/// Interaction states a widget can be in, used by pseudo-class selectors (CSS <c>:hover</c>,
/// <c>:active</c>, <c>:disabled</c>, <c>:focus</c>). A widget reports its current states via
/// <see cref="Radiant.UI.UIElement.CurrentPseudoState"/>.
/// </summary>
[Flags]
public enum PseudoState
{
    /// <summary>No interaction state.</summary>
    None = 0,

    /// <summary>The pointer is over the widget (<c>:hover</c>).</summary>
    Hover = 1 << 0,

    /// <summary>The widget is being pressed/activated (<c>:active</c>).</summary>
    Active = 1 << 1,

    /// <summary>The widget is disabled (<c>:disabled</c>).</summary>
    Disabled = 1 << 2,

    /// <summary>The widget holds keyboard focus (<c>:focus</c>).</summary>
    Focus = 1 << 3,
}
