namespace Radiant.Graphics2D;

/// <summary>The shape of a gradient's color field.</summary>
public enum GradientKind
{
    /// <summary>Colors change along a line, constant across it.</summary>
    Linear = 1,

    /// <summary>Colors change with distance from a center.</summary>
    Radial = 2,
}
