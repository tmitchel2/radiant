using System;
using Radiant.Animation;

namespace Radiant.Scrolling;

/// <summary>
/// The single-axis scroll physics kernel: offset/velocity state plus the
/// per-frame integration for momentum decay, rubber-band overscroll, snap
/// settling and spring-driven programmatic scroll. Sign-agnostic — callers feed
/// pre-signed offset deltas. One instance per scrollable axis.
/// </summary>
internal sealed class ScrollAxis
{
    /// <summary>Current scroll position (0 = content start; increases toward content end).</summary>
    public float Offset { get; private set; }

    /// <summary>Current velocity in content-units/second (signed like <see cref="Offset"/>).</summary>
    public float Velocity { get; private set; }

    /// <summary>Visible size on this axis.</summary>
    public float ViewportExtent { get; private set; }

    /// <summary>Total content size on this axis.</summary>
    public float ContentExtent { get; private set; }

    /// <summary>Current motion phase.</summary>
    public AxisPhase Phase { get; private set; } = AxisPhase.Idle;

    /// <summary>The furthest in-bounds offset.</summary>
    public float MaxOffset => MathF.Max(0f, ContentExtent - ViewportExtent);

    /// <summary>True while time-driven motion is in progress (host must keep ticking).</summary>
    public bool IsAnimating => Phase is AxisPhase.Momentum or AxisPhase.Animating;

    /// <summary>Overscroll distance past the nearest edge (for <see cref="OverscrollMode.Glow"/> rendering). Always ≥ 0.</summary>
    public float OverscrollAmount { get; private set; }

    private float _animTarget;
    private float _animVelocity;
    private float _springTime;

    public void SetExtents(float viewport, float content)
    {
        ViewportExtent = MathF.Max(0f, viewport);
        ContentExtent = MathF.Max(0f, content);
        if (Phase is AxisPhase.Idle or AxisPhase.Momentum)
            Offset = Math.Clamp(Offset, 0f, MaxOffset);
    }

    public void Reset()
    {
        Offset = 0f;
        Velocity = 0f;
        _animVelocity = 0f;
        OverscrollAmount = 0f;
        Phase = AxisPhase.Idle;
    }

    /// <summary>Instant wheel/line step (no momentum unless the behaviour opts in).</summary>
    public void ApplyImpulse(float offsetDelta, ScrollBehaviour b)
    {
        if (b.WheelMomentum)
        {
            Velocity += offsetDelta / MathF.Max(1e-4f, 0.016f); // treat one notch as ~one frame's velocity
            Phase = AxisPhase.Momentum;
            return;
        }

        Offset = Math.Clamp(Offset + offsetDelta, 0f, MaxOffset);
        Velocity = 0f;
        Phase = AxisPhase.Idle;
    }

    public void BeginDrag()
    {
        Velocity = 0f;
        Phase = AxisPhase.Dragging;
    }

    /// <summary>Apply a drag delta (content units) with rubber-band resistance past edges, tracking velocity.</summary>
    public void DragBy(float offsetDelta, double dt, ScrollBehaviour b)
    {
        Phase = AxisPhase.Dragging;
        var target = Offset + offsetDelta;

        if (b.Overscroll == OverscrollMode.Bounce)
        {
            if (target < 0f)
                Offset = -RubberBand(-target, ViewportExtent, b.RubberFactor);
            else if (target > MaxOffset)
                Offset = MaxOffset + RubberBand(target - MaxOffset, ViewportExtent, b.RubberFactor);
            else
                Offset = target;
        }
        else
        {
            Offset = Math.Clamp(target, 0f, MaxOffset);
        }

        var instantaneous = (float)(offsetDelta / MathF.Max(1e-4f, (float)dt));
        Velocity = (0.4f * Velocity) + (0.6f * instantaneous); // exponential smoothing
        UpdateOverscroll();
    }

    /// <summary>Decide what happens after the user lifts: bounce-return, snap, momentum, or rest.</summary>
    public void EndDrag(ScrollBehaviour b)
    {
        if (Offset < 0f || Offset > MaxOffset)
        {
            BeginSpring(Math.Clamp(Offset, 0f, MaxOffset), b.BounceSmoothTime, keepVelocity: true);
            return;
        }

        var snap = ResolveSnapTarget(b);
        if (snap is { } target)
        {
            BeginSpring(target, b.ScrollToSmoothTime, keepVelocity: false);
            return;
        }

        if (MathF.Abs(Velocity) > b.RestThreshold)
            Phase = AxisPhase.Momentum;
        else
            Settle();
    }

    /// <summary>Programmatic animated scroll to <paramref name="target"/>.</summary>
    public void AnimateTo(float target, ScrollBehaviour b) =>
        BeginSpring(Math.Clamp(target, 0f, MaxOffset), b.ScrollToSmoothTime, keepVelocity: false);

    /// <summary>Programmatic instant scroll to <paramref name="target"/>.</summary>
    public void JumpTo(float target)
    {
        Offset = Math.Clamp(target, 0f, MaxOffset);
        Velocity = 0f;
        _animVelocity = 0f;
        OverscrollAmount = 0f;
        Phase = AxisPhase.Idle;
    }

    /// <summary>Advance time-driven motion by one frame. Returns true if the offset changed.</summary>
    public bool Integrate(double dt, ScrollBehaviour b)
    {
        var before = Offset;

        switch (Phase)
        {
            case AxisPhase.Momentum:
                Velocity *= Decay.Factor(b.Deceleration.RatePerMs, dt);
                Offset += Velocity * (float)dt;

                if (Offset < 0f || Offset > MaxOffset)
                {
                    if (b.Overscroll == OverscrollMode.Bounce)
                        BeginSpring(Math.Clamp(Offset, 0f, MaxOffset), b.BounceSmoothTime, keepVelocity: true);
                    else
                    {
                        Offset = Math.Clamp(Offset, 0f, MaxOffset);
                        Velocity = 0f;
                        Settle();
                    }
                }
                else if (MathF.Abs(Velocity) < b.RestThreshold)
                {
                    Velocity = 0f;
                    var snap = ResolveSnapTarget(b);
                    if (snap is { } target)
                        BeginSpring(target, b.ScrollToSmoothTime, keepVelocity: false);
                    else
                        Settle();
                }
                break;

            case AxisPhase.Animating:
                Offset = SmoothDamp.Step(Offset, _animTarget, ref _animVelocity, _springTime, (float)dt);
                if (MathF.Abs(Offset - _animTarget) < 0.05f && MathF.Abs(_animVelocity) < b.RestThreshold)
                {
                    Offset = _animTarget;
                    _animVelocity = 0f;
                    Settle();
                }
                break;
        }

        UpdateOverscroll();
        return MathF.Abs(Offset - before) > 1e-4f;
    }

    private void BeginSpring(float target, float smoothTime, bool keepVelocity)
    {
        _animTarget = target;
        _springTime = smoothTime;
        _animVelocity = keepVelocity ? Velocity : 0f;
        Velocity = 0f;
        Phase = AxisPhase.Animating;
    }

    private void Settle()
    {
        Velocity = 0f;
        OverscrollAmount = 0f;
        Phase = AxisPhase.Idle;
    }

    private void UpdateOverscroll() =>
        OverscrollAmount =
            Offset < 0f ? -Offset
            : Offset > MaxOffset ? Offset - MaxOffset
            : 0f;

    /// <summary>
    /// Resolve the snap target for the projected resting offset, or null when no
    /// snapping applies. Uses the analytic momentum projection (not frame iteration).
    /// </summary>
    private float? ResolveSnapTarget(ScrollBehaviour b)
    {
        var paging = b.PagingEnabled;
        var snap = b.Snap;
        if (!paging && snap is null) return null;
        if (MaxOffset <= 0f) return null;

        var disableMomentum = snap?.DisableIntervalMomentum ?? false;
        var projected = disableMomentum
            ? Offset
            : Offset + Decay.ProjectedDistance(Velocity, b.Deceleration.RatePerMs);

        float target;
        if (snap?.Offsets is { Length: > 0 } offsets)
        {
            target = offsets[0];
            var best = MathF.Abs(offsets[0] - projected);
            foreach (var o in offsets)
            {
                var d = MathF.Abs(o - projected);
                if (d < best) { best = d; target = o; }
            }
        }
        else
        {
            var interval = paging ? ViewportExtent : (snap?.Interval ?? 0f);
            if (interval <= 0f) return null;

            var align = snap?.Alignment ?? SnapAlignment.Start;
            var bias = align switch
            {
                SnapAlignment.Center => ViewportExtent * 0.5f,
                SnapAlignment.End => ViewportExtent,
                _ => 0f,
            };
            target = (MathF.Round((projected + bias) / interval) * interval) - bias;
        }

        if (snap is { SnapToStart: false } && target <= 0f) return null;
        if (snap is { SnapToEnd: false } && target >= MaxOffset) return null;

        return Math.Clamp(target, 0f, MaxOffset);
    }

    // iOS rubber-band: resistance saturating at the viewport dimension.
    // b(x) = (x · dim · c) / (dim + c · x); as x→∞, b→dim.
    private static float RubberBand(float distance, float dimension, float c)
    {
        if (dimension <= 0f) return distance;
        return (distance * dimension * c) / (dimension + c * distance);
    }
}
