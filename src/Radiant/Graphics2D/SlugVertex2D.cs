using System.Numerics;
using System.Runtime.InteropServices;

namespace Radiant.Graphics2D;

/// <summary>
/// A corner of a glyph's quad drawn with Slug: where it is, where that is in the glyph's em square
/// (which the fragment shader evaluates the outline at), the colour, and where the glyph's data
/// starts in the curve and band buffers. Only <see cref="Position"/> moves under a transform; the
/// em coordinate stays, so the outline is evaluated exactly whatever the transform.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SlugVertex2D
{
    /// <summary>The position, in the coordinates it was drawn in (transformed at PopTransform).</summary>
    public Vector2 Position;

    /// <summary>The same point in em units relative to the pen, y up.</summary>
    public Vector2 Em;

    /// <summary>The straight-alpha linear colour.</summary>
    public Vector4 Color;

    /// <summary>The glyph's first word in the band buffer.</summary>
    public uint BandBase;

    /// <summary>The glyph's first texel in the curve buffer.</summary>
    public uint CurveBase;

    public SlugVertex2D(Vector2 position, Vector2 em, Vector4 color, SlugGlyphPlacement glyph)
    {
        Position = position;
        Em = em;
        Color = color;
        BandBase = glyph.BandBase;
        CurveBase = glyph.CurveBase;
    }
}
