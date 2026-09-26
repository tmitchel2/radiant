using System;

namespace Radiant.Text.Msdf;

/// <summary>
/// A glyph's multi-channel signed distance field, positioned like a <see cref="GlyphBitmap"/>:
/// relative to the pen on the baseline, in pixels at the size it was generated for, y down.
/// <para>
/// Each texel holds three distances, one per channel, stored as ½ + distance / <see cref="Range"/>
/// (in pixels, positive inside) and clamped to 0–255. The median of the three is the distance to
/// the outline, so a renderer draws where <c>median(r, g, b) &gt; ½</c>; because each channel
/// sees only some of the edges, corners stay sharp however far the field is magnified.
/// </para>
/// </summary>
public sealed class MsdfBitmap
{
    private readonly byte[] _pixels;

    internal MsdfBitmap(int left, int top, int width, int height, float range, byte[] pixels)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        Range = range;
        _pixels = pixels;
    }

    /// <summary>A field with no texels, for glyphs that draw nothing.</summary>
    public static MsdfBitmap Empty { get; } = new(0, 0, 0, 0, 0, []);

    /// <summary>The left edge's offset from the pen, in whole pixels.</summary>
    public int Left { get; }

    /// <summary>The top edge's offset from the baseline, in whole pixels (negative above it).</summary>
    public int Top { get; }

    /// <summary>The width in texels.</summary>
    public int Width { get; }

    /// <summary>The height in texels.</summary>
    public int Height { get; }

    /// <summary>
    /// The distance range in pixels (texels): the field encodes distances from −Range/2 to +Range/2.
    /// A renderer needs it to turn a sampled value into a distance in screen pixels.
    /// </summary>
    public float Range { get; }

    /// <summary>Whether the field has no texels.</summary>
    public bool IsEmpty => Width == 0 || Height == 0;

    /// <summary>RGBA texels, row by row from the top, <see cref="Width"/> × 4 bytes a row. Alpha is 255.</summary>
    public ReadOnlySpan<byte> Pixels => _pixels;

    /// <summary>
    /// The signed distance in pixels from a texel's centre to the outline, decoded from the median
    /// of its channels: positive inside, and clamped to ±<see cref="Range"/>/2.
    /// </summary>
    public float SignedDistance(int x, int y)
    {
        var i = (y * Width + x) * 4;
        int r = _pixels[i], g = _pixels[i + 1], b = _pixels[i + 2];
        var median = Math.Max(Math.Min(r, g), Math.Min(Math.Max(r, g), b));
        return (median / 255f - 0.5f) * Range;
    }
}
