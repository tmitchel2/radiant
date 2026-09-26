using System;
using System.Numerics;
using Radiant.Layout;

namespace Radiant.UI.Core;

/// <summary>
/// A box that draws itself with code: <see cref="Paint"/> is called with the renderer, the box's
/// top left in root coordinates, and its size. For charts, custom indicators and anything the
/// other elements can't express. It's drawn anew each frame; pass a new delegate (or change props)
/// when what it draws changes, so equality checks don't skip it.
/// </summary>
/// <param name="Paint">Draws the canvas.</param>
public sealed record Canvas(Action<PaintContext, Vector2, Vector2> Paint) : HostElement
{
    /// <summary>Size and placement.</summary>
    public LayoutStyle Layout { get; init; }

    /// <summary>What assistive technology should know about it.</summary>
    public Semantics? Semantics { get; init; }

    internal override RenderNode CreateRenderNode() => new CanvasRenderNode();
}
