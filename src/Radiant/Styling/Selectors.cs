using System;
using Radiant.UI;

namespace Radiant.Styling;

/// <summary>
/// Ergonomic builders for predicate selectors (<c>Func&lt;UIElement, bool&gt;</c>). Compose with
/// <see cref="And"/>/<see cref="Or"/> or bare <c>&amp;&amp;</c>/<c>||</c>:
/// <code>Selectors.OfType&lt;Button&gt;().And(Selectors.Hovered)</code>
/// </summary>
public static class Selectors
{
    /// <summary>Matches elements of (or derived from) type <typeparamref name="T"/>.</summary>
    public static Func<UIElement, bool> OfType<T>() where T : UIElement => e => e is T;

    /// <summary>Matches elements carrying the given style class.</summary>
    public static Func<UIElement, bool> Class(string name) => e => e.Classes.Contains(name);

    /// <summary>Matches every element.</summary>
    public static Func<UIElement, bool> Any() => _ => true;

    /// <summary>Matches hovered elements (<c>:hover</c>).</summary>
    public static Func<UIElement, bool> Hovered => e => e.CurrentPseudoState.HasFlag(PseudoState.Hover);

    /// <summary>Matches pressed/active elements (<c>:active</c>).</summary>
    public static Func<UIElement, bool> Pressed => e => e.CurrentPseudoState.HasFlag(PseudoState.Active);

    /// <summary>Matches disabled elements (<c>:disabled</c>).</summary>
    public static Func<UIElement, bool> Disabled => e => e.CurrentPseudoState.HasFlag(PseudoState.Disabled);

    /// <summary>Matches focused elements (<c>:focus</c>).</summary>
    public static Func<UIElement, bool> Focused => e => e.CurrentPseudoState.HasFlag(PseudoState.Focus);

    /// <summary>Logical AND of two selectors.</summary>
    public static Func<UIElement, bool> And(this Func<UIElement, bool> first, Func<UIElement, bool> second) =>
        e => first(e) && second(e);

    /// <summary>Logical OR of two selectors.</summary>
    public static Func<UIElement, bool> Or(this Func<UIElement, bool> first, Func<UIElement, bool> second) =>
        e => first(e) || second(e);
}
