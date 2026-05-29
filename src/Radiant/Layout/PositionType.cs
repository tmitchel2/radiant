namespace Radiant.Layout;

/// <summary>How an item is positioned relative to its flex flow (mirrors CSS <c>position</c>).</summary>
public enum PositionType
{
    /// <summary>Participates in normal flow; insets are ignored.</summary>
    Static,

    /// <summary>Participates in normal flow; insets offset it from its computed position (Yoga default).</summary>
    Relative,

    /// <summary>Removed from flow; positioned by insets relative to the containing block.</summary>
    Absolute,
}
