using System;
using System.Collections.Generic;
using System.Numerics;
using Facebook.Yoga;
using static Facebook.Yoga.YGNodeAPI;
using static Facebook.Yoga.YGNodeLayoutAPI;

namespace Radiant.UI.Core;

/// <summary>
/// What a host element becomes: a box that is laid out (by its own Yoga node, kept for its whole
/// life so layout is only redone where something changed), drawn and hit-tested. Render nodes
/// form a tree of host elements only; components in between leave no trace here.
/// </summary>
internal abstract class RenderNode : IDisposable
{
    private static int s_nextId;

    private readonly List<RenderNode> _children = [];

    /// <summary>A number for this node that no other node in the process shares, stable for its life.</summary>
    public int Id { get; } = System.Threading.Interlocked.Increment(ref s_nextId);

    protected RenderNode() => Yoga = YGNodeNew();

    public Node Yoga { get; }

    /// <summary>The element node that owns this render node.</summary>
    public ElementNode Owner { get; set; } = null!;

    public RenderNode? Parent { get; private set; }

    public IReadOnlyList<RenderNode> Children => _children;

    /// <summary>The top left, relative to the parent's top left.</summary>
    public Vector2 Position => new(YGNodeLayoutGetLeft(Yoga), YGNodeLayoutGetTop(Yoga));

    public Vector2 Size => new(YGNodeLayoutGetWidth(Yoga), YGNodeLayoutGetHeight(Yoga));

    /// <summary>A transform of this node and its children about its own top left, or null.</summary>
    public virtual Matrix3x2? LocalTransform => null;

    /// <summary>
    /// Where the children's coordinates start, relative to this node's top left: a scroll area
    /// moves its content by its scroll offset.
    /// </summary>
    public virtual Vector2 ChildOffset => Vector2.Zero;

    /// <summary>The Yoga node children are laid out in: this node's own, or one inside it.</summary>
    protected virtual Node ChildContainer => Yoga;

    /// <summary>Whether children are cut to this node's bounds (and so can't be hit outside them).</summary>
    public virtual bool ClipsChildren => false;

    /// <summary>Whether the pointer can hit this node itself (its children may still be hit).</summary>
    public virtual bool IsHitTestVisible => true;

    /// <summary>Brings the node up to date with a new element (the previous one is null the first time).</summary>
    public abstract void Update(HostElement element, HostElement? previous);

    /// <summary>Draws the node and its children, with its top left at <see cref="PaintContext.Origin"/>.</summary>
    public virtual void Paint(PaintContext context) => PaintChildren(context);

    protected void PaintChildren(PaintContext context) => context.PaintChildren(this);

    /// <summary>Replaces the children, in order, keeping the Yoga tree in step. Does nothing if unchanged.</summary>
    public void SetChildren(List<RenderNode> children)
    {
        if (children.Count == _children.Count)
        {
            var same = true;
            for (var i = 0; i < children.Count && same; i++)
            {
                same = ReferenceEquals(children[i], _children[i]);
            }
            if (same)
            {
                return;
            }
        }
        foreach (var child in _children)
        {
            child.Parent = null;
        }
        _children.Clear();
        _children.AddRange(children);
        var nodes = new Node[children.Count];
        for (var i = 0; i < children.Count; i++)
        {
            children[i].Parent = this;
            nodes[i] = children[i].Yoga;
        }
        YGNodeSetChildren(ChildContainer, nodes);
    }

    /// <summary>The top left in the root's coordinates (ignoring transforms), as of the last layout.</summary>
    public Vector2 AbsolutePosition
    {
        get
        {
            var origin = Position;
            for (var parent = Parent; parent is not null; parent = parent.Parent)
            {
                origin += parent.Position + parent.ChildOffset;
            }
            return origin;
        }
    }

    /// <summary>A point in the root's coordinates, in this node's own (its top left at the origin).</summary>
    public Vector2 ToLocal(Vector2 rootPoint)
    {
        var point = (Parent is null ? rootPoint : Parent.ToLocal(rootPoint) - Parent.ChildOffset) - Position;
        if (LocalTransform is { } transform && Matrix3x2.Invert(transform, out var inverse))
        {
            point = Vector2.Transform(point, inverse);
        }
        return point;
    }

    /// <summary>A point in this node's own coordinates, in the root's: the inverse of <see cref="ToLocal"/>.</summary>
    public Vector2 ToRoot(Vector2 localPoint)
    {
        var point = LocalTransform is { } transform ? Vector2.Transform(localPoint, transform) : localPoint;
        point += Position;
        return Parent is null ? point : Parent.ToRoot(point + Parent.ChildOffset);
    }

    /// <summary>The box around this node in the root's coordinates, after transforms, as of the last layout.</summary>
    public System.Drawing.RectangleF RootBounds()
    {
        var size = Size;
        if (LocalTransform is null && !HasTransformedAncestor())
        {
            var origin = AbsolutePosition;
            return new System.Drawing.RectangleF(origin.X, origin.Y, size.X, size.Y);
        }
        var a = ToRoot(Vector2.Zero);
        var b = ToRoot(new Vector2(size.X, 0));
        var c = ToRoot(new Vector2(0, size.Y));
        var d = ToRoot(size);
        var min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
        var max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
        return new System.Drawing.RectangleF(min.X, min.Y, max.X - min.X, max.Y - min.Y);
    }

    private bool HasTransformedAncestor()
    {
        for (var node = Parent; node is not null; node = node.Parent)
        {
            if (node.LocalTransform is not null)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The nodes under a point (in the parent's coordinates), topmost first, ending at the root
    /// of this subtree: later siblings are above earlier ones, children above their parent.
    /// </summary>
    public bool HitTest(Vector2 parentPoint, List<RenderNode> path)
    {
        var point = parentPoint - Position;
        if (LocalTransform is { } transform)
        {
            if (!Matrix3x2.Invert(transform, out var inverse))
            {
                return false;
            }
            point = Vector2.Transform(point, inverse);
        }
        var size = Size;
        var inside = point.X >= 0 && point.Y >= 0 && point.X < size.X && point.Y < size.Y;
        if (ClipsChildren && !inside)
        {
            return false;
        }
        if (inside && IsHitTestVisible && ClaimsPoint(point))
        {
            path.Add(this);
            return true;
        }
        var childPoint = point - ChildOffset;
        for (var i = _children.Count - 1; i >= 0; i--)
        {
            if (_children[i].HitTest(childPoint, path))
            {
                path.Add(this);
                return true;
            }
        }
        if (inside && IsHitTestVisible)
        {
            path.Add(this);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Whether a point (in this node's coordinates, inside it) is the node's own rather than its
    /// children's, even where a child is: a scroll area's scroll bar.
    /// </summary>
    protected virtual bool ClaimsPoint(Vector2 point) => false;

    /// <summary>What the node does with a wheel event nothing handled: a scroll area scrolls.</summary>
    public virtual void OnWheel(PointerEventArgs args)
    {
    }

    /// <summary>What the node does with a press its handlers left alone: a scroll area grabs its thumb.</summary>
    public virtual void OnPointerDown(PointerEventArgs args)
    {
    }

    /// <summary>What the node does with a move its handlers left alone (while pressed, wherever the pointer is).</summary>
    public virtual void OnPointerMove(PointerEventArgs args)
    {
    }

    /// <summary>What the node does with a release its handlers left alone.</summary>
    public virtual void OnPointerUp(PointerEventArgs args)
    {
    }

    public virtual void Dispose()
    {
        foreach (var child in _children)
        {
            child.Parent = null;
        }
        _children.Clear();
        Owner?.Root.TrackHug(Yoga, false);
        YGNodeFree(Yoga);
    }

    public override string ToString() => $"{GetType().Name} {Position} {Size}";
}
