namespace Radiant.Layout;

/// <summary>
/// Cross-axis alignment, used for both <c>align-items</c> (container) and
/// <c>align-self</c> (item) (mirrors CSS).
/// </summary>
public enum Align
{
    /// <summary>Defer to the parent's <c>align-items</c> (only meaningful for align-self).</summary>
    Auto,

    /// <summary>Align to the cross-axis start.</summary>
    FlexStart,

    /// <summary>Center on the cross axis.</summary>
    Center,

    /// <summary>Align to the cross-axis end.</summary>
    FlexEnd,

    /// <summary>Stretch to fill the cross axis (Yoga container default).</summary>
    Stretch,

    /// <summary>Align baselines.</summary>
    Baseline,

    /// <summary>Distribute lines with the first/last flush to the edges (multi-line only).</summary>
    SpaceBetween,

    /// <summary>Distribute lines with equal space around each (multi-line only).</summary>
    SpaceAround,

    /// <summary>Distribute lines with equal space between and at the edges (multi-line only).</summary>
    SpaceEvenly,
}
