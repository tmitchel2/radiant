namespace Radiant.Scrolling;

/// <summary>
/// Declarative scroll configuration — Radiant's analog of the React Native
/// ScrollView prop matrix. A single immutable record describing how a scroller
/// feels and behaves; consumed by <see cref="ScrollController"/>.
/// </summary>
public sealed record ScrollBehaviour
{
    /// <summary>Which axes scroll. Default <see cref="ScrollAxes.Vertical"/>.</summary>
    public ScrollAxes Axes { get; init; } = ScrollAxes.Vertical;

    /// <summary>On <see cref="ScrollAxes.Both"/>, lock to the dominant axis once a drag begins.</summary>
    public bool DirectionalLock { get; init; }

    /// <summary>When false, all scroll input is ignored.</summary>
    public bool ScrollEnabled { get; init; } = true;

    /// <summary>Content units moved per wheel notch.</summary>
    public float WheelStep { get; init; } = 28f;

    /// <summary>Content units moved per arrow-key press.</summary>
    public float LineStep { get; init; } = 28f;

    /// <summary>When true, wheel input imparts decaying momentum rather than an instant clamped step.</summary>
    public bool WheelMomentum { get; init; }

    /// <summary>Momentum deceleration preset/rate.</summary>
    public DecelerationRate Deceleration { get; init; } = DecelerationRate.Normal;

    /// <summary>Page-style snapping: snap to multiples of the viewport extent.</summary>
    public bool PagingEnabled { get; init; }

    /// <summary>Snap/paging configuration, or null for free scrolling.</summary>
    public SnapConfig? Snap { get; init; }

    /// <summary>Edge behaviour when over-scrolled.</summary>
    public OverscrollMode Overscroll { get; init; } = OverscrollMode.Bounce;

    /// <summary>iOS rubber-band resistance constant (smaller = stiffer).</summary>
    public float RubberFactor { get; init; } = 0.55f;

    /// <summary>Spring time for the bounce-back to an edge.</summary>
    public float BounceSmoothTime { get; init; } = 0.12f;

    /// <summary>Spring time for animated programmatic scroll and snap settle.</summary>
    public float ScrollToSmoothTime { get; init; } = 0.25f;

    /// <summary>Indicator (scrollbar) visibility policy.</summary>
    public IndicatorVisibility Indicators { get; init; } = IndicatorVisibility.Auto;

    /// <summary>Allow click-dragging the scrollbar thumb.</summary>
    public bool DraggableThumb { get; init; } = true;

    /// <summary>How long indicators flash after activity, in seconds.</summary>
    public float IndicatorFlashDuration { get; init; } = 0.75f;

    /// <summary>Speed (units/sec) below which momentum is considered at rest.</summary>
    public float RestThreshold { get; init; } = 5f;

    /// <summary>Pixels a drag must travel before a pan gesture activates.</summary>
    public float ActivationThreshold { get; init; } = 3f;

    /// <summary>
    /// Explicit content extent override (caller-managed, the legacy
    /// <c>ScrollPanel.ContentHeight</c> escape hatch). When null, the content
    /// extent is measured automatically. Interpreted on the primary scroll axis.
    /// </summary>
    public float? ContentExtentOverride { get; init; }
}
