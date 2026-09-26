using System;
using System.Numerics;
using Radiant.Layout;
using Radiant.UI.Core;

namespace Radiant.Components.Primitives;

/// <summary>
/// Floating content shown above everything, positioned against an anchor (a button's
/// <see cref="ElementRef"/>): the basis of menus, popovers, tooltips and selects. It measures itself,
/// places itself with <see cref="AnchoredPlacement"/>, and follows the anchor if it moves (a scroll).
/// It is hidden until it has been placed, so it never flashes in the wrong spot.
/// </summary>
/// <param name="Anchor">What to position against.</param>
/// <param name="Content">What floats.</param>
public sealed record Anchored(ElementRef Anchor, Element? Content) : Component
{
    /// <summary>The side of the anchor to sit on.</summary>
    public Side Side { get; init; } = Side.Bottom;

    /// <summary>How to line up along that side.</summary>
    public SideAlign Align { get; init; } = SideAlign.Start;

    /// <summary>The gap from the anchor.</summary>
    public float Offset { get; init; } = 4f;

    /// <summary>The closest the content comes to the viewport's edge.</summary>
    public float Padding { get; init; } = 8f;

    /// <summary>Whether to move to the other side when there isn't room.</summary>
    public bool Flip { get; init; } = true;

    /// <summary>Whether to slide along to stay inside the viewport.</summary>
    public bool Shift { get; init; } = true;

    /// <summary>Whether the content is at least as wide as the anchor (a select's list).</summary>
    public bool MatchAnchorWidth { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var content = context.UseRef(new ElementRef()).Value;
        var position = context.UseState<Vector2?>((Vector2?)null);
        var root = context.Root;
        var anchor = Anchor;
        var props = this;

        void Place()
        {
            if (!anchor.IsMounted || !content.IsMounted)
            {
                return;
            }
            var bounds = content.Bounds;
            var (placed, _) = AnchoredPlacement.Place(anchor.Bounds, new Vector2(bounds.Width, bounds.Height), root.Size,
                props.Side, props.Align, props.Offset, props.Padding, props.Flip, props.Shift);
            placed = new Vector2(MathF.Round(placed.X), MathF.Round(placed.Y));
            if (position.Value != placed)
            {
                position.Set(placed);
            }
        }

        // Placed after each layout, and checked every frame in case the anchor moved.
        context.UseEffect(() =>
        {
            Place();
            return null;
        });
        context.UseEffect(() => root.AddTicker(_ => Place()).Dispose, anchor);

        var at = position.Value ?? Vector2.Zero;
        var rightToLeft = context.UseRightToLeft();
        return new Portal(new Box
        {
            Ref = content,
            Opacity = position.Value is null ? 0f : 1f,
            Layout = new LayoutStyle
            {
                Position = PositionType.Absolute,
                // Placed at a point measured from the window's left, whichever way the UI reads.
                Inset = Edges.Physical(at.X, at.Y, Dimension.Undefined, Dimension.Undefined, rightToLeft),
                MinWidth = MatchAnchorWidth ? anchor.Bounds.Width : Dimension.Undefined,
            },
            Children = [Content],
        });
    }
}
