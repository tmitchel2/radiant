using System.Drawing;
using System.Numerics;
using Radiant.Graphics2D;

namespace Radiant.UI.Core;

/// <summary>
/// Paints the render tree into a <see cref="Renderer2D"/>. Nodes draw at absolute positions (the
/// running origin), so moving through the tree costs nothing; only real transforms push one.
/// <para>
/// Nodes that can't be seen aren't painted: the visible area starts as the viewport and narrows
/// to each clipping node (a scroll area, a clipped box) its children are inside, and a child whose
/// bounds, widened by <see cref="CullMargin"/> for shadows and small overflows, miss it is skipped
/// with everything inside it. Inside a transform nothing is skipped, as it could be moved into view.
/// </para>
/// </summary>
public sealed class PaintContext
{
    /// <summary>
    /// How far past its bounds a node may draw (a shadow, a focus ring, a child that overflows)
    /// and still be painted when its bounds themselves are out of view.
    /// </summary>
    public const float CullMargin = 32f;

    private RectangleF _visible;
    private int _transforms;

    internal PaintContext(Renderer2D renderer, Vector2 viewport)
    {
        Renderer = renderer;
        _visible = new RectangleF(0, 0, viewport.X, viewport.Y);
    }

    /// <summary>What to draw with.</summary>
    public Renderer2D Renderer { get; }

    /// <summary>The top left of the node being painted, in the root's coordinates.</summary>
    public Vector2 Origin { get; private set; }

    /// <summary>How many nodes were painted (for tests and profiling).</summary>
    internal int Painted { get; private set; }

    internal void PaintChildren(RenderNode node)
    {
        var saved = Origin;
        var savedVisible = _visible;
        if (node.ClipsChildren && _transforms == 0)
        {
            // Children are cut to this node: only what's inside it (and was visible) can be seen.
            _visible = RectangleF.Intersect(_visible, new RectangleF(saved.X, saved.Y, node.Size.X, node.Size.Y));
        }
        Origin = saved + node.ChildOffset;
        foreach (var child in node.Children)
        {
            if (_transforms == 0 && child.LocalTransform is null && !Visible(child))
            {
                continue;
            }
            Paint(child);
        }
        Origin = saved;
        _visible = savedVisible;
    }

    internal void Paint(RenderNode node)
    {
        Painted++;
        var saved = Origin;
        Origin = saved + node.Position;
        var transform = node.LocalTransform;
        if (transform is { } local)
        {
            // About the node's top left: to it, transform, and back.
            Renderer.PushTransform(Matrix3x2.CreateTranslation(-Origin) * local * Matrix3x2.CreateTranslation(Origin));
            _transforms++;
        }
        node.Paint(this);
        if (transform is not null)
        {
            Renderer.PopTransform();
            _transforms--;
        }
        Origin = saved;
    }

    // Whether a child (not yet painted, so its top left is the running origin plus its position)
    // could show in the visible area.
    private bool Visible(RenderNode child)
    {
        var at = Origin + child.Position;
        var size = child.Size;
        var bounds = new RectangleF(at.X - CullMargin, at.Y - CullMargin, size.X + 2 * CullMargin, size.Y + 2 * CullMargin);
        return bounds.IntersectsWith(_visible);
    }
}
