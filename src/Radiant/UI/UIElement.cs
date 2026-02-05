using System.Numerics;
using Radiant.Graphics2D;
using Radiant.Input;

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

    /// <summary>Whether the element is visible.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Whether the element is enabled for interaction.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Optional tag for identification.</summary>
    public object? Tag { get; set; }

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
