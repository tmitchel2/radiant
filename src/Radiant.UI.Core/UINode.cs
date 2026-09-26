using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;

namespace Radiant.UI.Core;

/// <summary>What kind of node a <see cref="UINode"/> is.</summary>
public enum UINodeKind
{
    /// <summary>The root.</summary>
    Root,

    /// <summary>A <see cref="Core.Box"/>.</summary>
    Box,

    /// <summary>A <see cref="TextBlock"/>.</summary>
    Text,

    /// <summary>An <see cref="Core.EditableText"/>.</summary>
    EditableText,

    /// <summary>A <see cref="ScrollArea"/>.</summary>
    Scroll,

    /// <summary>An <see cref="Core.Image"/>.</summary>
    Image,

    /// <summary>A <see cref="Core.Canvas"/>.</summary>
    Canvas,

    /// <summary>A <see cref="Core.Grid"/>.</summary>
    Grid,

    /// <summary>A <see cref="Core.Portal"/>'s layer.</summary>
    Portal,
}

/// <summary>Where a scroll area is scrolled to, and how far it can go.</summary>
/// <param name="Offset">How far the content is scrolled, from its start.</param>
/// <param name="MaxOffset">How far it can be scrolled.</param>
/// <param name="ContentSize">The content's size.</param>
/// <param name="ViewportSize">The size it's seen through.</param>
/// <param name="CanScrollHorizontal">Whether it scrolls sideways (and has somewhere to go).</param>
/// <param name="CanScrollVertical">Whether it scrolls up and down (and has somewhere to go).</param>
/// <param name="IsAnimating">Whether it's moving by itself: momentum, a bounce, an animated scroll.</param>
public sealed record ScrollInfo(Vector2 Offset, Vector2 MaxOffset, Vector2 ContentSize, Vector2 ViewportSize,
    bool CanScrollHorizontal, bool CanScrollVertical, bool IsAnimating);

/// <summary>
/// A laid-out node of a <see cref="UIRoot"/>'s tree, for inspecting it: where it is, what of it can be
/// seen and hit, and what it is. Its geometry is as of the last layout. Only use it on the UI thread,
/// and not across an update: a node may be gone after one (<see cref="IsMounted"/>).
/// </summary>
public sealed class UINode : IEquatable<UINode>
{
    private readonly UIRoot _root;

    internal UINode(UIRoot root, RenderNode node)
    {
        _root = root;
        Node = node;
    }

    internal RenderNode Node { get; }

    /// <summary>The node's id, as <see cref="SemanticsNode.Id"/> gives it: stable for the node's life, not across runs.</summary>
    public int Id => Node.Id;

    /// <summary>What kind of node it is.</summary>
    public UINodeKind Kind => Node switch
    {
        RootRenderNode => UINodeKind.Root,
        BoxRenderNode => UINodeKind.Box,
        TextRenderNode => UINodeKind.Text,
        EditableTextRenderNode => UINodeKind.EditableText,
        ScrollRenderNode => UINodeKind.Scroll,
        ImageRenderNode => UINodeKind.Image,
        CanvasRenderNode => UINodeKind.Canvas,
        GridRenderNode => UINodeKind.Grid,
        _ => UINodeKind.Portal,
    };

    /// <summary>Whether it's still in the tree.</summary>
    public bool IsMounted => Node.Owner is { Mounted: true } || Node is RootRenderNode;

    /// <summary>The node it's drawn in, or null for the root.</summary>
    public UINode? Parent => Node.Parent is { } parent ? new UINode(_root, parent) : null;

    /// <summary>The nodes drawn in it, back to front.</summary>
    public IReadOnlyList<UINode> Children
    {
        get
        {
            var children = new UINode[Node.Children.Count];
            for (var i = 0; i < children.Length; i++)
            {
                children[i] = new UINode(_root, Node.Children[i]);
            }
            return children;
        }
    }

    /// <summary>Where it is in the root's coordinates: the box around it, as transformed.</summary>
    public RectangleF Bounds => Node.RootBounds();

    /// <summary>
    /// The part of it that can be seen: its bounds cut by every clipping node it's drawn in and by the
    /// root; null if none of it can, or it's transparent.
    /// </summary>
    public RectangleF? VisibleBounds
    {
        get
        {
            if (EffectiveOpacity < 0.01f)
            {
                return null;
            }
            var visible = RectangleF.Intersect(Bounds, new RectangleF(0, 0, _root.Size.X, _root.Size.Y));
            for (var ancestor = Node.Parent; ancestor is not null && !visible.IsEmpty; ancestor = ancestor.Parent)
            {
                if (ancestor.ClipsChildren)
                {
                    visible = RectangleF.Intersect(visible, ancestor.RootBounds());
                }
            }
            return visible.Width <= 0 || visible.Height <= 0 ? null : visible;
        }
    }

    /// <summary>How much of it can be seen, from 0 to 1.</summary>
    public float VisibleRatio
    {
        get
        {
            var bounds = Bounds;
            var area = bounds.Width * bounds.Height;
            return VisibleBounds is { } visible && area > 0 ? Math.Clamp(visible.Width * visible.Height / area, 0f, 1f) : 0f;
        }
    }

    /// <summary>Its opacity after its ancestors': 0 is invisible.</summary>
    public float EffectiveOpacity
    {
        get
        {
            var opacity = 1f;
            for (RenderNode? node = Node; node is not null; node = node.Parent)
            {
                if (node is BoxRenderNode box)
                {
                    opacity *= Math.Clamp(box.Element.Opacity, 0f, 1f);
                }
            }
            return opacity;
        }
    }

    /// <summary>Whether the pointer can hit it (its children may be hit either way).</summary>
    public bool IsHitTestVisible => Node.IsHitTestVisible;

    /// <summary>Whether it cuts its children to its bounds.</summary>
    public bool ClipsChildren => Node.ClipsChildren;

    /// <summary>Whether it's transformed (scaled, rotated) itself.</summary>
    public bool HasTransform => Node.LocalTransform is not null;

    /// <summary>
    /// Its test ID: its <see cref="Semantics.TestId"/>, or the outermost <see cref="Element.TestId"/> of
    /// the element that made it and the components that are nothing but it.
    /// </summary>
    public string? TestId => UIRoot.TestIdOf(Node);

    /// <summary>What it tells assistive technology, if it says anything itself.</summary>
    public Semantics? Semantics => Node switch
    {
        BoxRenderNode box => box.Element.Semantics,
        ScrollRenderNode scroll => scroll.Element.Semantics,
        CanvasRenderNode canvas => canvas.Element.Semantics,
        _ => null,
    };

    /// <summary>Whether it can take keyboard focus.</summary>
    public bool IsFocusable => Node is BoxRenderNode { Element.Focusable: true };

    /// <summary>Whether it has keyboard focus.</summary>
    public bool IsFocused => ReferenceEquals(_root.FocusedNode, Node);

    /// <summary>The text it shows: a text block's, or an editable text's.</summary>
    public string? Text => Node switch
    {
        TextRenderNode text => text.Element.AttributedText.Text,
        EditableTextRenderNode editable => editable.Element.State.Text,
        _ => null,
    };

    /// <summary>For editable text: the selection.</summary>
    public TextSelection? Selection => Node is EditableTextRenderNode editable ? editable.Element.State.Selection : null;

    /// <summary>For editable text: what shows while it's empty.</summary>
    public string? Placeholder => Node is EditableTextRenderNode editable ? editable.Element.Placeholder : null;

    /// <summary>For a scroll area: where it's scrolled to.</summary>
    public ScrollInfo? Scroll => Node is ScrollRenderNode scroll
        ? new ScrollInfo(scroll.Controller.Offset, scroll.Controller.MaxOffset, scroll.Controller.ContentSize, scroll.Controller.ViewportSize,
            scroll.Controller.CanScrollHorizontal, scroll.Controller.CanScrollVertical, scroll.Controller.IsAnimating)
        : null;

    /// <summary>A point in the root's coordinates, in this node's own (its top left at the origin).</summary>
    public Vector2 ToLocal(Vector2 rootPoint) => Node.ToLocal(rootPoint);

    /// <summary>A point in this node's coordinates, in the root's.</summary>
    public Vector2 ToRoot(Vector2 localPoint) => Node.ToRoot(localPoint);

    /// <summary>Whether <paramref name="other"/> is drawn in this node, however deep.</summary>
    public bool IsAncestorOf(UINode other)
    {
        ArgumentNullException.ThrowIfNull(other);
        for (var node = other.Node.Parent; node is not null; node = node.Parent)
        {
            if (ReferenceEquals(node, Node))
            {
                return true;
            }
        }
        return false;
    }

    /// <inheritdoc/>
    public bool Equals(UINode? other) => other is not null && ReferenceEquals(Node, other.Node);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is UINode other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Node.GetHashCode();

    /// <summary>For debugging: kind, id, test ID and bounds.</summary>
    public override string ToString() => $"{Kind} #{Id}{(TestId is { } testId ? " @" + testId : "")} {Bounds}";
}
