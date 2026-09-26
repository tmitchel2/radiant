namespace Radiant.Layout;

/// <summary>
/// Per-edge lengths (margin / padding / position insets). Each edge defaults to
/// <see cref="Dimension.Undefined"/>, so a <c>default</c> value applies no overrides.
/// <para>
/// The sides are the start and end of a line, not left and right: left and right in a
/// left-to-right layout, right and left in a right-to-left one, so a layout mirrors as a whole when
/// its direction does (<c>LayoutStyle.Direction</c>).
/// </para>
/// </summary>
/// <param name="Start">The start side: left in a left-to-right layout.</param>
/// <param name="Top">The top.</param>
/// <param name="End">The end side: right in a left-to-right layout.</param>
/// <param name="Bottom">The bottom.</param>
public readonly record struct Edges(Dimension Start, Dimension Top, Dimension End, Dimension Bottom)
{
    /// <summary>No edges set.</summary>
    public static Edges None => default;

    /// <summary>The same length on all four edges.</summary>
    public static Edges All(Dimension value) => new(value, value, value, value);

    /// <summary>Horizontal (start and end) and vertical (top and bottom) lengths.</summary>
    public static Edges Symmetric(Dimension horizontal, Dimension vertical) =>
        new(horizontal, vertical, horizontal, vertical);

    /// <summary>
    /// Edges given as left and right (a point from pointer input or measured bounds) as start and
    /// end: in a layout that reads right to left, the left is the end.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="top">The top.</param>
    /// <param name="right">The right.</param>
    /// <param name="bottom">The bottom.</param>
    /// <param name="rightToLeft">Whether the layout they're in reads right to left.</param>
    public static Edges Physical(Dimension left, Dimension top, Dimension right, Dimension bottom, bool rightToLeft) =>
        rightToLeft ? new(right, top, left, bottom) : new(left, top, right, bottom);

    /// <summary>These edges with each edge <paramref name="over"/> sets replacing this one's.</summary>
    public Edges Merge(Edges over) => new(
        over.Start.IsSet ? over.Start : Start,
        over.Top.IsSet ? over.Top : Top,
        over.End.IsSet ? over.End : End,
        over.Bottom.IsSet ? over.Bottom : Bottom);
}
