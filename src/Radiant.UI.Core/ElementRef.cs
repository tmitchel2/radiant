using System.Drawing;

namespace Radiant.UI.Core;

/// <summary>
/// A handle on a laid-out box, given to it by <see cref="Box.Ref"/>: where it is on screen and
/// whether it has focus. For positioning things against it (a popover under its button) or
/// focusing it from code. Empty until the box is mounted and laid out.
/// </summary>
public sealed class ElementRef
{
    internal RenderNode? Node { get; set; }

    /// <summary>Whether a mounted box holds this ref.</summary>
    public bool IsMounted => Node is not null;

    /// <summary>
    /// The box's bounds in the root's coordinates, as of the last layout: its top left and size.
    /// For a transformed box, the untransformed rectangle's position.
    /// </summary>
    public RectangleF Bounds
    {
        get
        {
            if (Node is not { } node)
            {
                return RectangleF.Empty;
            }
            var origin = node.AbsolutePosition;
            var size = node.Size;
            return new RectangleF(origin.X, origin.Y, size.X, size.Y);
        }
    }

    /// <summary>Whether the box has keyboard focus.</summary>
    public bool IsFocused => Node is { } node && ReferenceEquals(node.Owner.Root.FocusedNode, node);

    /// <summary>Whether the box or something inside it has keyboard focus.</summary>
    public bool ContainsFocus
    {
        get
        {
            if (Node is not { } node)
            {
                return false;
            }
            for (var focused = node.Owner.Root.FocusedNode; focused is not null; focused = focused.Parent)
            {
                if (ReferenceEquals(focused, node))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Gives the box keyboard focus (showing the focus ring, as keyboard focus does), or, if it
    /// can't take focus itself, the first box inside it that can: a ref on a wrapper focuses the
    /// control it wraps.
    /// </summary>
    public void Focus(bool visible = true)
    {
        if (Node is { } node)
        {
            node.Owner.Root.Focus(FirstFocusable(node) ?? node, visible);
        }
    }

    private static RenderNode? FirstFocusable(RenderNode node)
    {
        if (node is BoxRenderNode { Element.Focusable: true })
        {
            return node;
        }
        foreach (var child in node.Children)
        {
            if (FirstFocusable(child) is { } found)
            {
                return found;
            }
        }
        return null;
    }
}
