namespace Radiant.Layout;

/// <summary>
/// Per-edge lengths (margin / padding / position insets). Each edge defaults to
/// <see cref="Dimension.Undefined"/>, so a <c>default</c> value applies no overrides.
/// </summary>
public readonly record struct Edges(Dimension Left, Dimension Top, Dimension Right, Dimension Bottom)
{
    /// <summary>No edges set.</summary>
    public static Edges None => default;

    /// <summary>The same length on all four edges.</summary>
    public static Edges All(Dimension value) => new(value, value, value, value);

    /// <summary>Horizontal (left/right) and vertical (top/bottom) lengths.</summary>
    public static Edges Symmetric(Dimension horizontal, Dimension vertical) =>
        new(horizontal, vertical, horizontal, vertical);
}
