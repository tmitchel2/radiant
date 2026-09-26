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

    // How far in from the edge the scroll bar can be grabbed.
    private const float GripWidth = 12f;

    private readonly Node _content = YGNodeNew();
    private ScrollController? _own;

    // The controller whose animations this node is listening for.
    private ScrollController? _listening;

    // While the thumb is dragged: the axis, and where on the thumb it was grabbed.
    private (bool Vertical, float Grab)? _thumbDrag;

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
        // An animated scroll asked for from outside (a list scrolling a row into view) needs frames.
        if (!ReferenceEquals(_listening, Controller))
        {
            if (_listening is not null)
            {
                _listening.AnimationStarted -= OnAnimationStarted;
            }
            _listening = Controller;
            _listening.AnimationStarted += OnAnimationStarted;
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

    protected override bool ClaimsPoint(Vector2 point) => BarAt(point) is not null;

    // Which scroll bar, if any, a point in this node is over: true for the vertical one.
    private bool? BarAt(Vector2 point)
    {
        if (Element.Behaviour.Indicators == IndicatorVisibility.None || Element.IndicatorColor.W <= 0f)
        {
            return null;
        }
        var size = Size;
        var controller = Controller;
        if (controller.CanScrollVertical && point.X >= size.X - GripWidth)
        {
            return true;
        }
        if (controller.CanScrollHorizontal && point.Y >= size.Y - GripWidth)
        {
            return false;
        }
        return null;
    }

    public override void OnPointerDown(PointerEventArgs args)
    {
        if (args.Button != PointerButton.Left || BarAt(ToLocal(args.Position)) is not { } vertical)
        {
            return;
        }
        var (along, viewport, content, offset) = Axis(vertical, ToLocal(args.Position));
        var (start, length) = Thumb(viewport, content, offset);
        // Grabbed on the thumb, it keeps its place under the pointer; on the track, the thumb
        // jumps to centre on the pointer first.
        var grab = along >= start && along <= start + length ? along - start : length / 2f;
        _thumbDrag = (vertical, grab);
        DragThumb(vertical, along, grab);
        args.Handled = true;
    }

    public override void OnPointerMove(PointerEventArgs args)
    {
        if (_thumbDrag is { } drag)
        {
            DragThumb(drag.Vertical, Axis(drag.Vertical, ToLocal(args.Position)).Along, drag.Grab);
            args.Handled = true;
        }
    }

    public override void OnPointerUp(PointerEventArgs args)
    {
        if (_thumbDrag is not null)
        {
            _thumbDrag = null;
            args.Handled = true;
        }
    }

    private (float Along, float Viewport, float Content, float Offset) Axis(bool vertical, Vector2 local)
    {
        var controller = Controller;
        return vertical
            ? (local.Y, Size.Y, controller.ContentSize.Y, controller.Offset.Y)
            : (local.X, Size.X, controller.ContentSize.X, controller.Offset.X);
    }

    // Puts the thumb's start at the pointer less the grab point, and scrolls to match.
    private void DragThumb(bool vertical, float along, float grab)
    {
        var (_, viewport, content, offset) = Axis(vertical, Vector2.Zero);
        var (_, length) = Thumb(viewport, content, offset);
        var track = viewport - 2f * IndicatorInset;
        var fraction = Math.Clamp((along - grab - IndicatorInset) / MathF.Max(track - length, 1f), 0f, 1f);
        var target = fraction * MathF.Max(content - viewport, 0f);
        var controller = Controller;
        controller.ScrollTo(vertical ? new Vector2(controller.Offset.X, target) : new Vector2(target, controller.Offset.Y), animated: false);
        Owner.Root.StartAnimating(this);
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

    private void OnAnimationStarted() => Owner.Root.StartAnimating(this);

    public override void Dispose()
    {
        if (_listening is not null)
        {
            _listening.AnimationStarted -= OnAnimationStarted;
        }
        Owner.Root.RemoveScroller(this);
        base.Dispose();
        YGNodeFree(_content);
    }
}
