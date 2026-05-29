namespace Radiant.Layout;

/// <summary>Unit of a <see cref="Dimension"/> value (mirrors Yoga's <c>YGUnit</c>).</summary>
public enum DimensionUnit
{
    /// <summary>Unset — the engine applies no override, so Yoga's default (usually auto) stands.</summary>
    Undefined,

    /// <summary>An absolute length in points (device-independent pixels here).</summary>
    Point,

    /// <summary>A percentage of the parent's corresponding dimension.</summary>
    Percent,

    /// <summary>Sized automatically by content / flex rules.</summary>
    Auto,
}
