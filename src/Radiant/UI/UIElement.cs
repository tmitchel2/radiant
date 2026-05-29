using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Graphics2D;
using Radiant.Input;
using Radiant.Layout;
using Radiant.Styling;

namespace Radiant.UI;

/// <summary>
/// Base class for UI elements.
/// </summary>
public abstract class UIElement
{
    /// <summary>Position of the element (top-left corner).</summary>
    public Vector2 Position { get; set; }

    /// <summary>Size of the element.</summary>
    public Vector2 Size { get; set; }

    /// <summary>
    /// Flexbox/box-model layout inputs. Applied by <see cref="YogaLayoutEngine"/> when a layout
    /// root opts in via <see cref="ParticipatesInLayout"/>. Unset properties keep Yoga's defaults.
    /// </summary>
    public LayoutStyle Layout { get; set; }

    /// <summary>
    /// When <c>true</c> on a top-level element, the layout engine computes this element's entire
    /// subtree's <see cref="Position"/>/<see cref="Size"/> from <see cref="Layout"/> each pass.
    /// Opt-in (default <c>false</c>): unset elements keep their manually assigned positions.
    /// </summary>
    public bool ParticipatesInLayout { get; set; }

    /// <summary>Whether the element is visible.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Whether the element is enabled for interaction.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Optional tag for identification.</summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Style classes used by selectors (CSS class-style matching). Mutable: <c>element.Classes.Add("primary")</c>.
    /// Distinct from <see cref="Tag"/>, which stays a single opaque identity token.
    /// </summary>
    public ICollection<string> Classes { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Inline paint overrides for this element, applied on top of any matching stylesheet rules
    /// (inline always wins). Leave properties unset to defer to the stylesheet / built-in defaults.
    /// </summary>
    public Style Style { get; set; }

    /// <summary>
    /// The effective style computed by <see cref="StyleResolver"/> during the style pass (stylesheet
    /// rules folded in cascade order, then inline <see cref="Style"/>). Widgets read this in
    /// <see cref="Draw"/>, falling back to their built-in colours for any unset property.
    /// </summary>
    public Style ResolvedStyle { get; set; }

    /// <summary>
    /// The element's current interaction states for pseudo-class selectors. The base implementation
    /// reports only <see cref="PseudoState.Disabled"/>; interactive widgets override to add
    /// hover/active/focus.
    /// </summary>
    public virtual PseudoState CurrentPseudoState => Enabled ? PseudoState.None : PseudoState.Disabled;

    /// <summary>Whether this element is currently capturing mouse input.</summary>
    public virtual bool IsCapturingInput => false;

    /// <summary>Gets the bounding rectangle of this element.</summary>
    public Rectangle Bounds => new(Position.X, Position.Y, Size.X, Size.Y);

    /// <summary>Checks if a point is within this element's bounds.</summary>
    public bool ContainsPoint(Vector2 point) =>
        point.X >= Position.X && point.X < Position.X + Size.X &&
        point.Y >= Position.Y && point.Y < Position.Y + Size.Y;

    /// <summary>Called when the element needs to be rendered.</summary>
    public abstract void Draw(Renderer2D renderer);

    /// <summary>Called after all siblings have drawn, for overlay content (e.g. dropdown lists).</summary>
    public virtual void DrawOverlay(Renderer2D renderer) { }

    /// <summary>Called to update the element state.</summary>
    public virtual void Update(InputState input, double deltaTime) { }

    /// <summary>Called when the mouse enters the element bounds.</summary>
    protected virtual void OnMouseEnter() { }

    /// <summary>Called when the mouse leaves the element bounds.</summary>
    protected virtual void OnMouseLeave() { }

    /// <summary>Called when a mouse button is pressed on this element.</summary>
    protected virtual void OnMouseDown(Vector2 localPosition) { }

    /// <summary>Called when a mouse button is released on this element.</summary>
    protected virtual void OnMouseUp(Vector2 localPosition) { }
}

/// <summary>
/// Simple rectangle struct for bounds checking.
/// </summary>
public readonly struct Rectangle
{
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }

    public Rectangle(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public bool Contains(Vector2 point) =>
        point.X >= X && point.X < X + Width &&
        point.Y >= Y && point.Y < Y + Height;
}
