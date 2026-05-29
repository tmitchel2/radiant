namespace Radiant.Scrolling;

/// <summary>
/// Snap/paging configuration. Mirrors React Native ScrollView's
/// <c>snapToInterval</c> / <c>snapToOffsets</c> / <c>snapToAlignment</c>.
/// When both <see cref="Offsets"/> and <see cref="Interval"/> are set,
/// <see cref="Offsets"/> wins.
/// </summary>
public sealed record SnapConfig
{
    /// <summary>Snap to integer multiples of this offset (content units). Ignored when <see cref="Offsets"/> is set.</summary>
    public float? Interval { get; init; }

    /// <summary>Explicit sorted snap offsets (content units). Takes precedence over <see cref="Interval"/>.</summary>
    public float[]? Offsets { get; init; }

    /// <summary>Where the snap point aligns within the viewport.</summary>
    public SnapAlignment Alignment { get; init; } = SnapAlignment.Start;

    /// <summary>Count offset 0 (content start) as a valid snap point.</summary>
    public bool SnapToStart { get; init; } = true;

    /// <summary>Count the maximum offset (content end) as a valid snap point.</summary>
    public bool SnapToEnd { get; init; } = true;

    /// <summary>
    /// When true, a flick stops at the next snap point regardless of velocity
    /// (no multi-cell momentum). When false, momentum is projected and the
    /// nearest snap point to the projected rest is chosen.
    /// </summary>
    public bool DisableIntervalMomentum { get; init; }
}
