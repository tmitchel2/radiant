using System.Drawing;
using System.Numerics;

namespace Radiant.Components.Primitives;

/// <summary>
/// Where floating content goes next to an anchor: on the chosen side and alignment, flipped to the
/// other side if it doesn't fit and there's more room there, and shifted along to stay inside the
/// viewport (as Floating UI's offset, flip and shift).
/// </summary>
public static class AnchoredPlacement
{
    /// <summary>The top left for content of <paramref name="size"/> next to <paramref name="anchor"/>, and the side it ended up on.</summary>
    public static (Vector2 Position, Side Side) Place(RectangleF anchor, Vector2 size, Vector2 viewport,
        Side side, SideAlign align, float offset, float padding, bool flip = true, bool shift = true)
    {
        if (flip)
        {
            side = Flipped(anchor, size, viewport, side, offset, padding);
        }
        var vertical = side is Side.Top or Side.Bottom;
        var position = side switch
        {
            Side.Top => new Vector2(0, anchor.Top - offset - size.Y),
            Side.Right => new Vector2(anchor.Right + offset, 0),
            Side.Left => new Vector2(anchor.Left - offset - size.X, 0),
            _ => new Vector2(0, anchor.Bottom + offset),
        };
        if (vertical)
        {
            position.X = align switch
            {
                SideAlign.Center => anchor.Left + (anchor.Width - size.X) / 2f,
                SideAlign.End => anchor.Right - size.X,
                _ => anchor.Left,
            };
        }
        else
        {
            position.Y = align switch
            {
                SideAlign.Center => anchor.Top + (anchor.Height - size.Y) / 2f,
                SideAlign.End => anchor.Bottom - size.Y,
                _ => anchor.Top,
            };
        }
        if (shift)
        {
            position = Vector2.Clamp(position,
                new Vector2(padding),
                Vector2.Max(new Vector2(padding), viewport - size - new Vector2(padding)));
        }
        return (position, side);
    }

    private static Side Flipped(RectangleF anchor, Vector2 size, Vector2 viewport, Side side, float offset, float padding)
    {
        var (room, opposite, needed) = side switch
        {
            Side.Top => (anchor.Top - padding, viewport.Y - anchor.Bottom - padding, size.Y + offset),
            Side.Right => (viewport.X - anchor.Right - padding, anchor.Left - padding, size.X + offset),
            Side.Left => (anchor.Left - padding, viewport.X - anchor.Right - padding, size.X + offset),
            _ => (viewport.Y - anchor.Bottom - padding, anchor.Top - padding, size.Y + offset),
        };
        if (room >= needed || opposite <= room)
        {
            return side;
        }
        return side switch
        {
            Side.Top => Side.Bottom,
            Side.Bottom => Side.Top,
            Side.Left => Side.Right,
            _ => Side.Left,
        };
    }
}
