namespace Radiant.Text;

/// <summary>A rectangle in a paragraph (pixels, y down), such as a caret or a piece of a selection.</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Right">The right edge (equal to <paramref name="Left"/> for a caret).</param>
/// <param name="Bottom">The bottom edge.</param>
/// <param name="Direction">The direction of the text the box covers.</param>
public readonly record struct TextBox(float Left, float Top, float Right, float Bottom, TextDirection Direction)
{
    /// <summary>The width.</summary>
    public float Width => Right - Left;

    /// <summary>The height.</summary>
    public float Height => Bottom - Top;
}
