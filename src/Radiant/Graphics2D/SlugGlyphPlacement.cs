using System.Numerics;

namespace Radiant.Graphics2D;

/// <summary>Where a glyph's Slug data sits in the <see cref="SlugGlyphCache"/>'s buffers, and its em-space box.</summary>
/// <param name="BandBase">The glyph's first word in the band buffer.</param>
/// <param name="CurveBase">The glyph's first texel in the curve buffer.</param>
/// <param name="Min">The bottom left of the glyph's box, in em units, y up.</param>
/// <param name="Max">The top right of the glyph's box.</param>
internal readonly record struct SlugGlyphPlacement(uint BandBase, uint CurveBase, Vector2 Min, Vector2 Max)
{
    /// <summary>A glyph with no outline (a space): nothing to draw.</summary>
    public static SlugGlyphPlacement Empty { get; } = new(0, 0, Vector2.Zero, Vector2.Zero);

    /// <summary>Whether there is nothing to draw.</summary>
    public bool IsEmpty => Min == Max;
}
