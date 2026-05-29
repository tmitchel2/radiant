using System.Numerics;

namespace Radiant.Styling;

/// <summary>
/// Resolved paint properties for a widget. Every property is nullable so "unset" is distinct from a
/// value: cascade merging (<see cref="Merge"/>) only overrides properties the winning rule actually
/// set, and widgets fall back to their built-in defaults (e.g. <c>UIColors</c>) for anything unset.
///
/// <para>Scope is intentionally paint-only (colours, border, radius, text). Layout lives on
/// <see cref="Radiant.Layout.LayoutStyle"/> via <see cref="Radiant.UI.UIElement.Layout"/>, keeping
/// the style pass and the layout pass decoupled. Stylesheet-driven layout is a documented follow-up.</para>
/// </summary>
public readonly record struct Style
{
    /// <summary>Fill colour of the element's box.</summary>
    public Vector4? BackgroundColor { get; init; }

    /// <summary>Border stroke colour.</summary>
    public Vector4? BorderColor { get; init; }

    /// <summary>Border stroke width in points.</summary>
    public float? BorderWidth { get; init; }

    /// <summary>Corner radius in points (consumed by the rounded-rect renderer).</summary>
    public float? BorderRadius { get; init; }

    /// <summary>Text/foreground colour.</summary>
    public Vector4? TextColor { get; init; }

    /// <summary>Text scale multiplier.</summary>
    public float? TextScale { get; init; }

    /// <summary>
    /// Returns <paramref name="over"/> layered on <paramref name="under"/>: each property prefers
    /// the over value when set, otherwise keeps the under value. Used to fold matched rules in
    /// cascade order (later/higher-priority rules are passed as <paramref name="over"/>).
    /// </summary>
    public static Style Merge(Style under, Style over) => new()
    {
        BackgroundColor = over.BackgroundColor ?? under.BackgroundColor,
        BorderColor = over.BorderColor ?? under.BorderColor,
        BorderWidth = over.BorderWidth ?? under.BorderWidth,
        BorderRadius = over.BorderRadius ?? under.BorderRadius,
        TextColor = over.TextColor ?? under.TextColor,
        TextScale = over.TextScale ?? under.TextScale,
    };
}
