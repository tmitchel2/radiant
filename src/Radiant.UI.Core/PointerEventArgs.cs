using System.Numerics;

namespace Radiant.UI.Core;

/// <summary>A mouse, trackpad or pen event.</summary>
public sealed class PointerEventArgs : UIEventArgs
{
    internal PointerEventArgs(Vector2 position, PointerButton button, KeyModifiers modifiers, int clickCount = 0, Vector2 wheelDelta = default)
    {
        Position = position;
        Button = button;
        Modifiers = modifiers;
        ClickCount = clickCount;
        WheelDelta = wheelDelta;
    }

    /// <summary>Where the pointer is, in the root's coordinates.</summary>
    public Vector2 Position { get; }

    /// <summary>Where the pointer is relative to the top left of the box handling the event.</summary>
    public Vector2 LocalPosition { get; internal set; }

    /// <summary>The button pressed or released (for moves and wheels, the left button).</summary>
    public PointerButton Button { get; }

    /// <summary>The modifier keys held.</summary>
    public KeyModifiers Modifiers { get; }

    /// <summary>For clicks and presses: 1 for a single click, 2 for a double click, and so on.</summary>
    public int ClickCount { get; }

    /// <summary>For wheel events: how far to scroll, in pixels; positive scrolls towards the content's end (down, right).</summary>
    public Vector2 WheelDelta { get; }
}
