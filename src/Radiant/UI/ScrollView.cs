using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Animation;
using Radiant.Gestures;
using Radiant.Graphics2D;
using Radiant.Input;
using Radiant.Layout;
using Radiant.Scrolling;
using Silk.NET.Input;

namespace Radiant.UI;

/// <summary>
/// Scrollable container backed by the shared <see cref="ScrollController"/> physics
/// engine. Unlike the legacy <see cref="ScrollPanel"/> (which physically shifts each
/// child's position), <see cref="ScrollView"/> keeps children at their natural layout
/// positions and scrolls by a <em>render-time translate</em>
/// (<see cref="Renderer2D.PushScrollOffset"/>) — O(1) per frame, sub-pixel safe, and
/// able to express momentum, bounce, snap and animated programmatic scroll.
///
/// Input sources: mouse wheel, drag-to-pan, a draggable scrollbar thumb (+ track
/// paging), and the keyboard (PageUp/Down/Home/End/arrows while hovered). Children are
/// hit-tested in content space (the pointer is shifted by the offset while updating
/// them) and a hovered, scrollable child takes priority (nested-scroll yield).
/// </summary>
public class ScrollView : UIElement, IUiContainer, ILayoutBoundary, IAnimating
{
    private readonly List<UIElement> _children = [];
    private readonly PanGesture _pan;
    private readonly PanGesture _thumbPan;
    private readonly TapGesture _trackTap;
    private readonly GestureDetector _detector;
    private bool _mouseOver;
    private float _idleTime;

    public ScrollView(ScrollBehaviour? behaviour = null)
    {
        Controller = new ScrollController(behaviour ?? new ScrollBehaviour());

        // Draggable scrollbar thumb (highest priority — its hit area is the thumb rect).
        _thumbPan = new PanGesture { Axis = ScrollAxes.Vertical, ActivationThreshold = 1f, HitArea = HitThumb };
        _thumbPan.OnBegin = _ => Controller.BeginDrag();
        _thumbPan.OnChange = g => DragThumb(g.FrameDelta.Y);
        _thumbPan.OnEnd = _ => Controller.EndDrag();
        _thumbPan.OnCancel = _ => Controller.EndDrag();

        // Track click pages toward the click.
        _trackTap = new TapGesture { HitArea = HitTrack };
        _trackTap.OnEnd = g => PageToward(g.Position.Y);

        // Drag-to-pan the body.
        _pan = new PanGesture
        {
            Axis = Controller.Behaviour.Axes,
            ActivationThreshold = Controller.Behaviour.ActivationThreshold,
        };
        _pan.OnBegin = _ => Controller.BeginDrag();
        _pan.OnChange = g => Controller.Drag(g.FrameDelta, g.Dt);
        _pan.OnEnd = _ => Controller.EndDrag();
        _pan.OnCancel = _ => Controller.EndDrag();

        _detector = new GestureDetector(_thumbPan, _trackTap, _pan);
    }

    /// <summary>The scroll physics engine. Subscribe to its events; call its programmatic API.</summary>
    public ScrollController Controller { get; }

    public Vector4 BackgroundColor { get; set; } = UIColors.Background;
    public bool DrawBackground { get; set; } = true;
    public float ScrollbarWidth { get; set; } = 6f;

    public IReadOnlyList<UIElement> Children => _children;

    /// <inheritdoc/>
    public bool IsAnimating => Controller.IsAnimating;

    public override bool IsCapturingInput
    {
        get
        {
            if (_detector.HasActiveOrClaimingOwner) return true;
            if (_mouseOver && (Controller.CanScrollVertical || Controller.CanScrollHorizontal))
                return true;
            foreach (var c in _children)
                if (c.IsCapturingInput) return true;
            return false;
        }
    }

    public T Add<T>(T element) where T : UIElement
    {
        _children.Add(element);
        return element;
    }

    public void Clear()
    {
        _children.Clear();
        Controller.ScrollTo(Vector2.Zero, animated: false);
    }

    /// <summary>Scroll to an absolute content offset.</summary>
    public void ScrollTo(Vector2 offset, bool animated) => Controller.ScrollTo(offset, animated);

    /// <summary>Reset the scroll position to the origin.</summary>
    public void ResetScroll() => Controller.ScrollTo(Vector2.Zero, animated: false);

    public override void Update(InputState input, double deltaTime)
    {
        if (!Visible || !Enabled) return;

        _mouseOver = ContainsPoint(input.MousePosition);
        Controller.SetExtents(Size, MeasureContentSize());

        // Children first, in content space; a hovered scrollable child takes the input.
        var savedMouse = input.MousePosition;
        input.MousePosition = savedMouse + Controller.Offset;
        var childCaptured = UpdateChildren(input, deltaTime);
        input.MousePosition = savedMouse;

        // Drag/keyboard yield to a hovered scrollable child; an in-progress drag of our own
        // keeps priority.
        if (!childCaptured || _detector.Owner != null)
        {
            _detector.Update(PointerFrame.From(input, deltaTime), _mouseOver);
            HandleKeyboard(input);
        }

        // Wheel arbitration is by consumption (ScrollDelta zeroing), not the drag-yield gate:
        // an inner scroller zeroes the delta when it consumes; an unconsumed notch (inner at
        // its boundary) still reaches us here.
        ApplyWheelWithBoundary(input);

        Controller.Update(deltaTime);
        UpdateIndicatorFade(deltaTime);
    }

    private bool UpdateChildren(InputState input, double deltaTime)
    {
        for (var i = _children.Count - 1; i >= 0; i--)
        {
            if (_children[i].Visible && _children[i].IsCapturingInput)
            {
                _children[i].Update(input, deltaTime);
                return true;
            }
        }

        for (var i = _children.Count - 1; i >= 0; i--)
            if (_children[i].Visible)
                _children[i].Update(input, deltaTime);

        for (var i = 0; i < _children.Count; i++)
            if (_children[i].Visible && _children[i].IsCapturingInput)
                return true;
        return false;
    }

    private void HandleKeyboard(InputState input)
    {
        if (!_mouseOver || !Controller.CanScrollVertical) return;
        var line = Controller.Behaviour.LineStep;
        if (input.IsKeyPressed(Key.PageDown)) Controller.ApplyStep(new Vector2(0, Size.Y));
        if (input.IsKeyPressed(Key.PageUp)) Controller.ApplyStep(new Vector2(0, -Size.Y));
        if (input.IsKeyPressed(Key.Down)) Controller.ApplyStep(new Vector2(0, line));
        if (input.IsKeyPressed(Key.Up)) Controller.ApplyStep(new Vector2(0, -line));
        if (input.IsKeyPressed(Key.Home)) Controller.ScrollTo(Vector2.Zero, animated: true);
        if (input.IsKeyPressed(Key.End)) Controller.ScrollToEnd(animated: true);
    }

    private void ApplyWheelWithBoundary(InputState input)
    {
        if (!_mouseOver) return;
        var wheel = input.ScrollDelta;

        // Consume by zeroing ScrollDelta so a parent scroller doesn't also scroll. A notch
        // that would only push past an edge is left unconsumed, propagating to the parent
        // (nested-scroll boundary handoff).
        if (MathF.Abs(wheel.Y) > 0.001f && Controller.CanScrollVertical
            && CanConsumeWheel(wheel.Y, Controller.Offset.Y, Controller.MaxOffset.Y))
        {
            Controller.ApplyWheel(new Vector2(0, wheel.Y));
            input.ScrollDelta = new Vector2(input.ScrollDelta.X, 0);
        }
        if (MathF.Abs(wheel.X) > 0.001f && Controller.CanScrollHorizontal
            && CanConsumeWheel(wheel.X, Controller.Offset.X, Controller.MaxOffset.X))
        {
            Controller.ApplyWheel(new Vector2(wheel.X, 0));
            input.ScrollDelta = new Vector2(0, input.ScrollDelta.Y);
        }
    }

    // Don't consume a wheel notch that would only push further past an edge, so it can
    // propagate to a parent scroller (basic nested-scroll handoff).
    private bool CanConsumeWheel(float wheel, float offset, float maxOffset)
    {
        var intended = -wheel * Controller.Behaviour.WheelStep;
        if (intended < 0f && offset <= 0.5f) return false;
        if (intended > 0f && offset >= maxOffset - 0.5f) return false;
        return true;
    }

    private void DragThumb(float thumbDeltaY)
    {
        var thumbHeight = ThumbHeight();
        var travel = Size.Y - thumbHeight;
        if (travel <= 0f) return;
        var offsetDelta = thumbDeltaY * (Controller.MaxOffset.Y / travel);
        Controller.ScrollTo(new Vector2(Controller.Offset.X, Controller.Offset.Y + offsetDelta), animated: false);
    }

    private void PageToward(float clickY)
    {
        var (_, thumbY, thumbHeight) = ThumbGeometry();
        var page = clickY < thumbY ? -Size.Y : clickY > thumbY + thumbHeight ? Size.Y : 0f;
        if (page != 0f) Controller.ApplyStep(new Vector2(0, page));
    }

    public override void Draw(Renderer2D renderer)
    {
        if (!Visible) return;

        if (DrawBackground)
            renderer.DrawFilledRect(Position, Size, BackgroundColor);

        renderer.PushClip(Position.X, Position.Y, Size.X, Size.Y);
        renderer.PushScrollOffset(-Controller.Offset);
        foreach (var c in _children)
            if (c.Visible) c.Draw(renderer);
        renderer.PopScrollOffset();
        renderer.PopClip();

        DrawScrollbar(renderer);
    }

    public override void DrawOverlay(Renderer2D renderer)
    {
        renderer.PushScrollOffset(-Controller.Offset);
        foreach (var c in _children)
            if (c.Visible) c.DrawOverlay(renderer);
        renderer.PopScrollOffset();
    }

    private Vector2 MeasureContentSize()
    {
        var overrideExtent = Controller.Behaviour.ContentExtentOverride;
        var width = Size.X;
        var height = Size.Y;

        if (overrideExtent is { } ov)
        {
            if (Controller.Behaviour.Axes == ScrollAxes.Horizontal) width = ov;
            else height = ov;
            return new Vector2(width, height);
        }

        var maxRight = 0f;
        var maxBottom = 0f;
        foreach (var c in _children)
        {
            if (!c.Visible) continue;
            maxRight = MathF.Max(maxRight, (c.Position.X - Position.X) + c.Size.X);
            maxBottom = MathF.Max(maxBottom, (c.Position.Y - Position.Y) + c.Size.Y);
        }
        return new Vector2(MathF.Max(width, maxRight), MathF.Max(height, maxBottom));
    }

    private float ThumbHeight()
    {
        var content = Controller.ContentSize.Y;
        return content <= 0f ? Size.Y : MathF.Max(20f, Size.Y * (Size.Y / content));
    }

    private (float trackX, float thumbY, float thumbHeight) ThumbGeometry()
    {
        var maxScroll = Controller.MaxOffset.Y;
        var thumbHeight = ThumbHeight();
        var trackX = Position.X + Size.X - ScrollbarWidth;
        var thumbY = maxScroll > 0f
            ? Position.Y + (Controller.Offset.Y / maxScroll) * (Size.Y - thumbHeight)
            : Position.Y;
        return (trackX, thumbY, thumbHeight);
    }

    private bool HitThumb(Vector2 p)
    {
        if (!Controller.CanScrollVertical) return false;
        var (trackX, thumbY, thumbHeight) = ThumbGeometry();
        return p.X >= trackX && p.X <= trackX + ScrollbarWidth && p.Y >= thumbY && p.Y <= thumbY + thumbHeight;
    }

    private bool HitTrack(Vector2 p)
    {
        if (!Controller.CanScrollVertical) return false;
        var trackX = Position.X + Size.X - ScrollbarWidth;
        var onTrack = p.X >= trackX && p.X <= trackX + ScrollbarWidth && p.Y >= Position.Y && p.Y <= Position.Y + Size.Y;
        return onTrack && !HitThumb(p);
    }

    private void UpdateIndicatorFade(double dt)
    {
        var active = _mouseOver || _detector.Owner != null || Controller.IsAnimating;
        _idleTime = active ? 0f : _idleTime + (float)dt;
    }

    private float IndicatorAlpha()
    {
        if (Controller.Behaviour.Indicators == IndicatorVisibility.Always) return 1f;
        var fade = MathF.Max(0.0001f, Controller.Behaviour.IndicatorFlashDuration);
        return Math.Clamp(1f - _idleTime / fade, 0f, 1f);
    }

    private void DrawScrollbar(Renderer2D renderer)
    {
        if (Controller.Behaviour.Indicators == IndicatorVisibility.None) return;
        if (!Controller.CanScrollVertical) return;

        var alpha = IndicatorAlpha();
        if (alpha <= 0.001f) return;

        var (trackX, thumbY, thumbHeight) = ThumbGeometry();
        renderer.DrawFilledRect(
            new Vector2(trackX, Position.Y),
            new Vector2(ScrollbarWidth, Size.Y),
            UIColors.BackgroundLight * new Vector4(1, 1, 1, alpha));
        renderer.DrawFilledRect(
            new Vector2(trackX, thumbY),
            new Vector2(ScrollbarWidth, thumbHeight),
            UIColors.Border * new Vector4(1, 1, 1, alpha));
    }
}
