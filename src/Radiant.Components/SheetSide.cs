namespace Radiant.Components;

/// <summary>Which edge of the window a <see cref="Sheet"/> slides in from.</summary>
public enum SheetSide
{
    /// <summary>The end edge: the right, or the left in a right-to-left UI.</summary>
    End,

    /// <summary>The start edge: the left, or the right in a right-to-left UI.</summary>
    Start,

    /// <summary>The bottom edge.</summary>
    Bottom,
}
