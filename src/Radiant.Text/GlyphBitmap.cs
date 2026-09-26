using System;

namespace Radiant.Text;

/// <summary>
/// A glyph rasterized to 8-bit coverage: 0 outside, 255 inside, the fraction of each pixel the
/// outline covers in between. Positioned relative to the pen on the baseline, in pixels, y down.
/// </summary>
public sealed class GlyphBitmap
{
    private readonly byte[] _coverage;

    internal GlyphBitmap(int left, int top, int width, int height, byte[] coverage)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        _coverage = coverage;
    }

    /// <summary>A bitmap with no pixels, for glyphs that draw nothing.</summary>
    public static GlyphBitmap Empty { get; } = new(0, 0, 0, 0, []);

    /// <summary>The left edge's offset from the pen, in whole pixels.</summary>
    public int Left { get; }

    /// <summary>The top edge's offset from the baseline, in whole pixels (negative above it).</summary>
    public int Top { get; }

    /// <summary>The width in pixels.</summary>
    public int Width { get; }

    /// <summary>The height in pixels.</summary>
    public int Height { get; }

    /// <summary>Whether the bitmap has no pixels.</summary>
    public bool IsEmpty => Width == 0 || Height == 0;

    /// <summary>Coverage, row by row from the top, <see cref="Width"/> bytes a row.</summary>
    public ReadOnlySpan<byte> Coverage => _coverage;

    /// <summary>The coverage of one pixel.</summary>
    public byte this[int x, int y] => _coverage[y * Width + x];
}
