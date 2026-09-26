using System;
using System.Numerics;

namespace Radiant.Text.Slug;

/// <summary>
/// A glyph prepared for Slug (Eric Lengyel, "GPU-Centered Font Rendering Directly from Glyph
/// Outlines", JCGT 2017): its quadratic curves and the bands that say which curves a pixel's rays
/// can cross, packed flat so they upload to the GPU as they are. Coordinates are in em units, y up,
/// relative to the pen on the baseline, so one prepared glyph serves every size and transform.
/// <para>
/// <b>Curves</b> are four-float texels. A curve at texel <c>i</c> has its first two control points
/// in texel <c>i</c> (x, y, z, w) and its third in the x and y of texel <c>i + 1</c>. A contour's
/// curves follow on, so each shares a texel with the next; after a contour's last curve (or a
/// curve that doesn't end where the next begins) comes one texel holding just its end point.
/// </para>
/// <para>
/// <b>Bands</b> are 32-bit words, laid out as:
/// <list type="bullet">
/// <item><description>
/// A header of <see cref="HeaderLength"/> words: the number of horizontal bands, the number of
/// vertical bands, then the bits of four floats: the band scale in x and y and the band offset in x
/// and y. A sample at em position <c>p</c> is in vertical band <c>floor(p.x * scale.x + offset.x)</c>
/// and horizontal band <c>floor(p.y * scale.y + offset.y)</c>, clamped to the bands there are.
/// </description></item>
/// <item><description>
/// Two words per horizontal band (bottom to top), then two per vertical band (left to right): how
/// many curves the band holds and where its list starts, counted in words from the header.
/// </description></item>
/// <item><description>
/// The lists: curve texel indexes. A horizontal band's curves are sorted by their greatest x,
/// largest first, so a ray cast right can stop at the first curve wholly behind the pixel; a
/// vertical band's by their greatest y.
/// </description></item>
/// </list>
/// </para>
/// </summary>
public sealed class SlugGlyph
{
    /// <summary>The number of words before the band headers.</summary>
    public const int HeaderLength = 6;

    private readonly Vector4[] _curves;
    private readonly uint[] _bands;

    internal SlugGlyph(Vector4[] curves, uint[] bands, int curveCount, Vector2 min, Vector2 max)
    {
        _curves = curves;
        _bands = bands;
        CurveCount = curveCount;
        Min = min;
        Max = max;
    }

    /// <summary>A glyph that draws nothing, such as a space.</summary>
    public static SlugGlyph Empty { get; } = new([], [], 0, Vector2.Zero, Vector2.Zero);

    /// <summary>Whether the glyph draws nothing.</summary>
    public bool IsEmpty => CurveCount == 0;

    /// <summary>The number of quadratic curves in the outline.</summary>
    public int CurveCount { get; }

    /// <summary>The bottom left of the box around every control point, in em units.</summary>
    public Vector2 Min { get; }

    /// <summary>The top right of the box around every control point, in em units.</summary>
    public Vector2 Max { get; }

    /// <summary>The number of horizontal bands (each a slice of the glyph's height).</summary>
    public int HorizontalBandCount => IsEmpty ? 0 : (int)_bands[0];

    /// <summary>The number of vertical bands (each a slice of the glyph's width).</summary>
    public int VerticalBandCount => IsEmpty ? 0 : (int)_bands[1];

    /// <summary>The curve texels, as described on the class.</summary>
    public ReadOnlySpan<Vector4> Curves => _curves;

    /// <summary>The band words, as described on the class.</summary>
    public ReadOnlySpan<uint> Bands => _bands;

    /// <summary>The most curves any one band holds: the most a pixel's ray visits.</summary>
    public int MaxCurvesPerBand
    {
        get
        {
            var most = 0;
            var bands = HorizontalBandCount + VerticalBandCount;
            for (var i = 0; i < bands; i++)
            {
                most = Math.Max(most, (int)_bands[HeaderLength + (2 * i)]);
            }
            return most;
        }
    }
}
