namespace Radiant.Graphics2D;

/// <summary>Where a glyph's coverage is in a <see cref="GlyphAtlas"/>, and where it goes relative to the pen.</summary>
/// <param name="Page">The atlas page.</param>
/// <param name="X">The left texel of the glyph (inside its border).</param>
/// <param name="Y">The top texel.</param>
/// <param name="Width">The width in texels (and device pixels).</param>
/// <param name="Height">The height.</param>
/// <param name="Left">The bitmap's left edge from the pen, in device pixels.</param>
/// <param name="Top">The bitmap's top edge from the baseline, in device pixels (negative above).</param>
internal readonly record struct AtlasGlyph(int Page, int X, int Y, int Width, int Height, int Left, int Top)
{
    /// <summary>A glyph that draws nothing (a space), or one too large for the atlas.</summary>
    public static AtlasGlyph Empty { get; } = new(-1, 0, 0, 0, 0, 0, 0);

    /// <summary>Whether there is nothing to draw.</summary>
    public bool IsEmpty => Width == 0 || Height == 0;
}
