using System.Numerics;
using Radiant.Graphics2D;

namespace Radiant.UI.Core;

/// <summary>
/// Paints the render tree into a <see cref="Renderer2D"/>. Nodes draw at absolute positions (the
/// running origin), so moving through the tree costs nothing; only real transforms push one.
/// </summary>
public sealed class PaintContext
{
    internal PaintContext(Renderer2D renderer) => Renderer = renderer;

    /// <summary>What to draw with.</summary>
    public Renderer2D Renderer { get; }

    /// <summary>The top left of the node being painted, in the root's coordinates.</summary>
    public Vector2 Origin { get; private set; }

    internal void PaintChildren(RenderNode node)
    {
        var saved = Origin;
        Origin = saved + node.ChildOffset;
        foreach (var child in node.Children)
        {
            Paint(child);
        }
        Origin = saved;
    }

    internal void Paint(RenderNode node)
    {
        var saved = Origin;
        Origin = saved + node.Position;
        var transform = node.LocalTransform;
        if (transform is { } local)
        {
            // About the node's top left: to it, transform, and back.
            Renderer.PushTransform(Matrix3x2.CreateTranslation(-Origin) * local * Matrix3x2.CreateTranslation(Origin));
        }
        node.Paint(this);
        if (transform is not null)
        {
            Renderer.PopTransform();
        }
        Origin = saved;
    }
}
