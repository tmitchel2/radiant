using System;
using System.Numerics;
using Facebook.Yoga;
using Radiant.Graphics2D;
using Radiant.Scrolling;
using static Facebook.Yoga.YGNodeAPI;
using static Facebook.Yoga.YGNodeLayoutAPI;
using static Facebook.Yoga.YGNodeStyleAPI;

namespace Radiant.UI.Core;

/// <summary>
/// Lays out, draws and scrolls a <see cref="ScrollArea"/>. Children are laid out in a content
/// node inside the viewport, and Yoga's scroll overflow leaves them unconstrained along the
/// scrolling axes; the content is then drawn and hit-tested moved by the scroll offset.
/// </summary>
internal sealed class ScrollRenderNode : RenderNode
{
    private const float IndicatorThickness = 4f;
    private const float IndicatorInset = 2f;

    private readonly Node _content = YGNodeNew();
    private ScrollController? _own;

    public ScrollRenderNode() => YGNodeInsertChild(Yoga, _content, 0);

    public ScrollArea Element { get; private set; } = null!;

    public ScrollController Controller => Element.Controller ?? (_own ??= new ScrollController(Element.Behaviour));

    public override Vector2 ChildOffset =>
        new Vector2(YGNodeLayoutGetLeft(_content), YGNodeLayoutGetTop(_content)) - Controller.Offset;

    public override bool ClipsChildren => true;

    protected override Node ChildContainer => _content;

    public override void Update(HostElement element, HostElement? previous)
    {
        Element = (ScrollArea)element;
        var old = previous as ScrollArea;
        if (_own is not null)
        {
            _own.Behaviour = Element.Behaviour;
        }
        if (old is null)
        {
            Owner.Root.AddScroller(this);
        }
        if (old is null || old.Layout != Element.Layout || old.ContentLayout != Element.ContentLayout || old.Behaviour.Axes != Element.Behaviour.Axes)
        {
            var axes = Element.Behaviour.Axes;
            var vertical = axes is ScrollAxes.Vertical or ScrollAxes.Both;
            var horizontal = axes is ScrollAxes.Horizontal or ScrollAxes.Both;

            YogaStyle.Set(Yoga, Element.Layout, reset: old is not null);
            if (Element.Layout.FlexShrink is null)
            {
                // As in CSS, a scroll container shrinks to fit rather than growing to its content.
                YGNodeStyleSetFlexShrink(Yoga, 1f);
            }
            // Scroll overflow lets the content outgrow the viewport along the main axis; the
            // direction makes the scrolling axis the main one (both: column, content-sized across).
            YGNodeStyleSetOverflow(Yoga, YGOverflow.Scroll);
            YGNodeStyleSetFlexDirection(Yoga, horizontal && !vertical ? YGFlexDirection.Row : YGFlexDirection.Column);

            YogaStyle.Set(_content, Element.ContentLayout, reset: old is not null);
            // The content keeps its full size along the scrolling axis, and grows to fill the
            // viewport when it's shorter (so it can centre or push things to the end).
            YGNodeStyleSetFlexShrink(_content, 0f);
            YGNodeStyleSetFlexGrow(_content, 1f);
            YGNodeStyleSetAlignSelf(_content, horizontal && vertical ? YGAlign.FlexStart : YGAlign.Stretch);
        }
    }

    /// <summary>Tells the controller the viewport and content sizes from the latest layout.</summary>
    public void SyncExtents() =>
        Controller.SetExtents(Size, new Vector2(YGNodeLayoutGetWidth(_content), YGNodeLayoutGetHeight(_content)));

    /// <summary>Advances momentum, bounce and animated scrolling; false once at rest.</summary>
    public bool Advance(double seconds)
    {
        Controller.Update(seconds);
        return Controller.IsAnimating;
    }

    public override void OnWheel(PointerEventArgs args)
    {
        var delta = args.WheelDelta;
        var controller = Controller;
        // Scroll only along an axis that can still move that way; otherwise an enclosing area should.
        var canY = delta.Y != 0f && controller.CanScrollVertical
            && (delta.Y > 0f ? controller.Offset.Y < controller.MaxOffset.Y - 0.5f : controller.Offset.Y > 0.5f);
        var canX = delta.X != 0f && controller.CanScrollHorizontal
            && (delta.X > 0f ? controller.Offset.X < controller.MaxOffset.X - 0.5f : controller.Offset.X > 0.5f);
        if (!canY && !canX)
        {
            return;
        }
        // The controller takes wheel notches (up positive) and scrolls WheelStep pixels each: in
        // those units, a delta of d pixels towards the end is -d / WheelStep notches.
        var step = MathF.Max(Element.Behaviour.WheelStep, 1e-3f);
        controller.ApplyWheel(new Vector2(canX ? -delta.X / step : 0f, canY ? -delta.Y / step : 0f));
        Owner.Root.StartAnimating(this);
        args.Handled = true;
    }

    public override void Paint(PaintContext context)
    {
        var renderer = context.Renderer;
        var (x, y) = (context.Origin.X, context.Origin.Y);
        var size = Size;
        renderer.PushClip(x, y, size.X, size.Y);
        PaintChildren(context);
        renderer.PopClip();
        PaintIndicators(renderer, context.Origin, size);
    }

    private void PaintIndicators(Renderer2D renderer, Vector2 origin, Vector2 size)
    {
        var color = Element.IndicatorColor;
        if (color.W <= 0f || Element.Behaviour.Indicators == IndicatorVisibility.None)
        {
            return;
        }
        var controller = Controller;
        if (controller.CanScrollVertical)
        {
            var (start, length) = Thumb(size.Y, controller.ContentSize.Y, controller.Offset.Y);
            renderer.DrawRoundedRectFilled(origin.X + size.X - IndicatorThickness - IndicatorInset, origin.Y + start,
                IndicatorThickness, length, IndicatorThickness / 2f, color);
        }
        if (controller.CanScrollHorizontal)
        {
            var (start, length) = Thumb(size.X, controller.ContentSize.X, controller.Offset.X);
            renderer.DrawRoundedRectFilled(origin.X + start, origin.Y + size.Y - IndicatorThickness - IndicatorInset,
                length, IndicatorThickness, IndicatorThickness / 2f, color);
        }
    }

    // The thumb's length is the visible fraction of the track; its position, the scrolled fraction.
    private static (float Start, float Length) Thumb(float viewport, float content, float offset)
    {
        var track = viewport - 2f * IndicatorInset;
        var length = MathF.Max(track * viewport / MathF.Max(content, 1f), 24f);
        var travel = MathF.Max(content - viewport, 1f);
        var start = IndicatorInset + (track - length) * Math.Clamp(offset / travel, 0f, 1f);
        return (start, length);
    }

    public override void Dispose()
    {
        Owner.Root.RemoveScroller(this);
        base.Dispose();
        YGNodeFree(_content);
    }
}
