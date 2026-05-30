using System;
using System.Numerics;
using Radiant.Animation;

namespace Radiant.Scrolling;

/// <summary>
/// The shared scroll engine: composes one or two <see cref="ScrollAxis"/> kernels,
/// applies a <see cref="ScrollBehaviour"/>, exposes a direct input API
/// (wheel/drag/programmatic), integrates physics each frame, and raises the
/// React-Native-style scroll lifecycle events. Widgets own a controller and
/// delegate all scroll state to it.
/// </summary>
public sealed class ScrollController : IAnimating
{
    private readonly ScrollAxis _x = new();
    private readonly ScrollAxis _y = new();

    private bool _dragging;
    private bool _wasMomentum;

    public ScrollController(ScrollBehaviour behaviour) => Behaviour = behaviour;

    /// <summary>The active behaviour. Replaceable at runtime.</summary>
    public ScrollBehaviour Behaviour { get; set; }

    /// <summary>Current scroll offset (content units). X used for horizontal, Y for vertical.</summary>
    public Vector2 Offset => new(_x.Offset, _y.Offset);

    /// <summary>Current scroll velocity (content units/second).</summary>
    public Vector2 Velocity => new(_x.Velocity, _y.Velocity);

    /// <summary>Total content size in content units.</summary>
    public Vector2 ContentSize => new(_x.ContentExtent, _y.ContentExtent);

    /// <summary>Visible viewport size.</summary>
    public Vector2 ViewportSize => new(_x.ViewportExtent, _y.ViewportExtent);

    /// <summary>Furthest in-bounds offset per axis.</summary>
    public Vector2 MaxOffset => new(_x.MaxOffset, _y.MaxOffset);

    /// <summary>Overscroll distance past the nearest edge per axis (for glow rendering).</summary>
    public Vector2 OverscrollAmount => new(_x.OverscrollAmount, _y.OverscrollAmount);

    /// <inheritdoc/>
    public bool IsAnimating => HorizontalEnabled && _x.IsAnimating || VerticalEnabled && _y.IsAnimating;

    /// <summary>True while the content can scroll on the vertical axis.</summary>
    public bool CanScrollVertical => VerticalEnabled && _y.MaxOffset > 0f;

    /// <summary>True while the content can scroll on the horizontal axis.</summary>
    public bool CanScrollHorizontal => HorizontalEnabled && _x.MaxOffset > 0f;

    public event Action<ScrollMetrics>? Scroll;
    public event Action<ScrollMetrics>? ScrollBeginDrag;
    public event Action<ScrollMetrics>? ScrollEndDrag;
    public event Action<ScrollMetrics>? MomentumBegin;
    public event Action<ScrollMetrics>? MomentumEnd;

    private bool VerticalEnabled => Behaviour.Axes is ScrollAxes.Vertical or ScrollAxes.Both;
    private bool HorizontalEnabled => Behaviour.Axes is ScrollAxes.Horizontal or ScrollAxes.Both;

    /// <summary>Set the visible viewport and total content extents.</summary>
    public void SetExtents(Vector2 viewport, Vector2 content)
    {
        _x.SetExtents(viewport.X, content.X);
        _y.SetExtents(viewport.Y, content.Y);
    }

    /// <summary>Apply a raw wheel delta (notches). Vertical wheel scrolls Y; X scrolls horizontally.</summary>
    public void ApplyWheel(Vector2 wheelDelta)
    {
        if (!Behaviour.ScrollEnabled) return;
        if (VerticalEnabled && MathF.Abs(wheelDelta.Y) > 1e-4f)
            _y.ApplyImpulse(-wheelDelta.Y * Behaviour.WheelStep, Behaviour);
        if (HorizontalEnabled && MathF.Abs(wheelDelta.X) > 1e-4f)
            _x.ApplyImpulse(-wheelDelta.X * Behaviour.WheelStep, Behaviour);
    }

    /// <summary>Apply a keyboard line/page step (positive scrolls toward content end).</summary>
    public void ApplyStep(Vector2 step)
    {
        if (!Behaviour.ScrollEnabled) return;
        if (VerticalEnabled && MathF.Abs(step.Y) > 1e-4f) _y.AnimateTo(_y.Offset + step.Y, Behaviour);
        if (HorizontalEnabled && MathF.Abs(step.X) > 1e-4f) _x.AnimateTo(_x.Offset + step.X, Behaviour);
    }

    public void BeginDrag()
    {
        if (!Behaviour.ScrollEnabled) return;
        _dragging = true;
        if (VerticalEnabled) _y.BeginDrag();
        if (HorizontalEnabled) _x.BeginDrag();
        ScrollBeginDrag?.Invoke(MakeArgs());
    }

    /// <summary>Drag the content by a pixel delta (pointer movement). Content follows the pointer.</summary>
    public void Drag(Vector2 deltaPixels, double dt)
    {
        if (!Behaviour.ScrollEnabled || !_dragging) return;
        // Content moves opposite to the pointer: dragging down reveals earlier content (offset decreases).
        if (VerticalEnabled) _y.DragBy(-deltaPixels.Y, dt, Behaviour);
        if (HorizontalEnabled) _x.DragBy(-deltaPixels.X, dt, Behaviour);
        Scroll?.Invoke(MakeArgs());
    }

    public void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        if (VerticalEnabled) _y.EndDrag(Behaviour);
        if (HorizontalEnabled) _x.EndDrag(Behaviour);
        ScrollEndDrag?.Invoke(MakeArgs());
    }

    /// <summary>Scroll to an absolute offset, optionally animated.</summary>
    public void ScrollTo(Vector2 target, bool animated)
    {
        if (animated)
        {
            if (VerticalEnabled) _y.AnimateTo(target.Y, Behaviour);
            if (HorizontalEnabled) _x.AnimateTo(target.X, Behaviour);
        }
        else
        {
            if (VerticalEnabled) _y.JumpTo(target.Y);
            if (HorizontalEnabled) _x.JumpTo(target.X);
        }
    }

    /// <summary>Scroll to the content end on the enabled axes.</summary>
    public void ScrollToEnd(bool animated) => ScrollTo(MaxOffset, animated);

    /// <summary>Advance physics by one frame and raise lifecycle events at phase edges.</summary>
    public void Update(double dt)
    {
        // Clamp dt so a post-idle hiccup can't teleport the spring or overshoot momentum.
        var clamped = Math.Min(dt, 1.0 / 30.0);

        var wasAnimatingBefore = IsAnimating;
        var changed = false;
        if (VerticalEnabled) changed |= _y.Integrate(clamped, Behaviour);
        if (HorizontalEnabled) changed |= _x.Integrate(clamped, Behaviour);

        var isMomentum = (VerticalEnabled && _y.Phase == AxisPhase.Momentum)
                         || (HorizontalEnabled && _x.Phase == AxisPhase.Momentum);
        if (isMomentum && !_wasMomentum) MomentumBegin?.Invoke(MakeArgs());
        if (!isMomentum && _wasMomentum) MomentumEnd?.Invoke(MakeArgs());
        _wasMomentum = isMomentum;

        if (changed) Scroll?.Invoke(MakeArgs());

        // Defensive: if motion just ended this frame, ensure a final Scroll fired.
        if (wasAnimatingBefore && !IsAnimating && !changed) Scroll?.Invoke(MakeArgs());
    }

    private ScrollMetrics MakeArgs() => new(Offset, ContentSize, ViewportSize, Velocity);
}
