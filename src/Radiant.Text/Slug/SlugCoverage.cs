// The coverage calculation follows Eric Lengyel's reference Slug pixel shader
// (https://github.com/EricLengyel/Slug, MIT licence, Copyright 2017 Eric Lengyel; see
// THIRD-PARTY-NOTICES.md).

using System;
using System.Numerics;

namespace Radiant.Text.Slug;

/// <summary>
/// The coverage Slug gives a pixel, computed on the CPU exactly as Radiant's Slug shader computes
/// it on the GPU, in single precision and step for step. It exists so the method can be tested
/// without a GPU, and so the shader has a readable twin.
/// <para>
/// A pixel casts one ray right and one ray up from its centre. Each curve in the pixel's band that
/// the ray crosses adds or takes away the fraction of the pixel before the crossing, which sums
/// to a winding number blurred over one pixel: anti-aliasing along the ray. Which crossings count
/// is decided by the signs of the curve's control points alone (the paper's "root eligibility"
/// lookup, 0x2E74), never by testing the roots, which is what makes the method robust where
/// curves meet. The two rays' results are averaged, each weighted by how near a crossing came to
/// the pixel centre, and the non-zero fill rule takes the magnitude, clamped to one, so
/// overlapping contours fill as one.
/// </para>
/// </summary>
public static class SlugCoverage
{
    // Below this |a| (em units) a curve's y(t) is taken to be linear.
    private const float NearlyLinear = 1f / 65536f;

    /// <summary>The coverage of the pixel centred on <paramref name="sample"/>.</summary>
    /// <param name="glyph">The prepared glyph.</param>
    /// <param name="sample">The pixel centre in em units, y up.</param>
    /// <param name="pixelsPerEm">
    /// How many pixels an em spans along x and along y: what the shader gets from the derivatives
    /// of its em coordinates.
    /// </param>
    /// <returns>Coverage from 0 to 1.</returns>
    public static float Evaluate(SlugGlyph glyph, Vector2 sample, Vector2 pixelsPerEm)
    {
        ArgumentNullException.ThrowIfNull(glyph);
        if (glyph.IsEmpty)
        {
            return 0f;
        }
        var curves = glyph.Curves;
        var bands = glyph.Bands;
        var hCount = (int)bands[0];
        var vCount = (int)bands[1];
        var scale = new Vector2(BitConverter.UInt32BitsToSingle(bands[2]), BitConverter.UInt32BitsToSingle(bands[3]));
        var offset = new Vector2(BitConverter.UInt32BitsToSingle(bands[4]), BitConverter.UInt32BitsToSingle(bands[5]));
        var bandX = Math.Clamp((int)MathF.Floor((sample.X * scale.X) + offset.X), 0, vCount - 1);
        var bandY = Math.Clamp((int)MathF.Floor((sample.Y * scale.Y) + offset.Y), 0, hCount - 1);

        // The horizontal band's curves, against a ray cast right.
        var xcov = 0f;
        var xwgt = 0f;
        var hHeader = SlugGlyph.HeaderLength + (2 * bandY);
        var hList = (int)bands[hHeader + 1];
        for (var i = 0; i < (int)bands[hHeader]; i++)
        {
            var at = (int)bands[hList + i];
            var p12 = curves[at] - new Vector4(sample, sample.X, sample.Y);
            var p3 = new Vector2(curves[at + 1].X, curves[at + 1].Y) - sample;
            if (MathF.Max(MathF.Max(p12.X, p12.Z), p3.X) * pixelsPerEm.X < -0.5f)
            {
                break; // sorted by greatest x: every curve from here is wholly behind the pixel
            }
            var code = RootCode(p12.Y, p12.W, p3.Y);
            if (code != 0)
            {
                var r = SolveHorizontal(p12, p3) * pixelsPerEm.X;
                if ((code & 1) != 0)
                {
                    xcov += Saturate(r.X + 0.5f);
                    xwgt = MathF.Max(xwgt, Saturate(1f - (MathF.Abs(r.X) * 2f)));
                }
                if (code > 1)
                {
                    xcov -= Saturate(r.Y + 0.5f);
                    xwgt = MathF.Max(xwgt, Saturate(1f - (MathF.Abs(r.Y) * 2f)));
                }
            }
        }

        // The vertical band's curves, against a ray cast up.
        var ycov = 0f;
        var ywgt = 0f;
        var vHeader = SlugGlyph.HeaderLength + (2 * hCount) + (2 * bandX);
        var vList = (int)bands[vHeader + 1];
        for (var i = 0; i < (int)bands[vHeader]; i++)
        {
            var at = (int)bands[vList + i];
            var p12 = curves[at] - new Vector4(sample, sample.X, sample.Y);
            var p3 = new Vector2(curves[at + 1].X, curves[at + 1].Y) - sample;
            if (MathF.Max(MathF.Max(p12.Y, p12.W), p3.Y) * pixelsPerEm.Y < -0.5f)
            {
                break;
            }
            var code = RootCode(p12.X, p12.Z, p3.X);
            if (code != 0)
            {
                var r = SolveVertical(p12, p3) * pixelsPerEm.Y;
                if ((code & 1) != 0)
                {
                    ycov -= Saturate(r.X + 0.5f);
                    ywgt = MathF.Max(ywgt, Saturate(1f - (MathF.Abs(r.X) * 2f)));
                }
                if (code > 1)
                {
                    ycov += Saturate(r.Y + 0.5f);
                    ywgt = MathF.Max(ywgt, Saturate(1f - (MathF.Abs(r.Y) * 2f)));
                }
            }
        }

        return Combine(xcov, ycov, xwgt, ywgt);
    }

    /// <summary>
    /// The two rays' coverages combined: averaged by weight (how near each ray's nearest crossing
    /// came to the pixel centre), but never less than the smaller of the two, and clamped to one
    /// for the non-zero fill rule. Magnitudes, so either winding direction fills.
    /// </summary>
    internal static float Combine(float xcov, float ycov, float xwgt, float ywgt) =>
        Saturate(MathF.Max(
            MathF.Abs((xcov * xwgt) + (ycov * ywgt)) / MathF.Max(xwgt + ywgt, 1f / 65536f),
            MathF.Min(MathF.Abs(xcov), MathF.Abs(ycov))));

    /// <summary>
    /// Which of a curve's two roots count, from the signs of its control points' coordinates across
    /// the ray (relative to the sample): bit 0 for the first root (which adds), bit 8 for the
    /// second (which subtracts). The paper's Table 1 as a 16-bit lookup, indexed by sign bits.
    /// </summary>
    internal static uint RootCode(float y1, float y2, float y3)
    {
        var i1 = BitConverter.SingleToUInt32Bits(y1) >> 31;
        var i2 = BitConverter.SingleToUInt32Bits(y2) >> 30;
        var i3 = BitConverter.SingleToUInt32Bits(y3) >> 29;
        var shift = (i2 & 2u) | (i1 & ~2u);
        shift = (i3 & 4u) | (shift & ~4u);
        return (0x2E74u >> (int)shift) & 0x0101u;
    }

    /// <summary>
    /// The x of the two places a sample-relative curve crosses y = 0. With a = p1 - 2 p2 + p3 and
    /// b = p1 - p2, y(t) = a t² - 2 b t + p1.y; a negative discriminant is clamped to zero (a
    /// double root at the turning point), and a nearly linear curve solves -2 b t + p1.y = 0.
    /// Divisors are guarded so no infinity is ever made, as the shader must avoid.
    /// </summary>
    internal static Vector2 SolveHorizontal(Vector4 p12, Vector2 p3)
    {
        var a = new Vector2(p12.X - (p12.Z * 2f) + p3.X, p12.Y - (p12.W * 2f) + p3.Y);
        var b = new Vector2(p12.X - p12.Z, p12.Y - p12.W);
        var linear = MathF.Abs(a.Y) < NearlyLinear;
        var ra = 1f / (linear ? 1f : a.Y);
        var rb = 0.5f / (b.Y == 0f ? 1f : b.Y);
        var d = MathF.Sqrt(MathF.Max((b.Y * b.Y) - (a.Y * p12.Y), 0f));
        var t1 = linear ? p12.Y * rb : (b.Y - d) * ra;
        var t2 = linear ? p12.Y * rb : (b.Y + d) * ra;
        return new Vector2(
            (((a.X * t1) - (b.X * 2f)) * t1) + p12.X,
            (((a.X * t2) - (b.X * 2f)) * t2) + p12.X);
    }

    /// <summary>As <see cref="SolveHorizontal"/>, with x and y swapped: the y where the curve crosses x = 0.</summary>
    internal static Vector2 SolveVertical(Vector4 p12, Vector2 p3)
    {
        var a = new Vector2(p12.X - (p12.Z * 2f) + p3.X, p12.Y - (p12.W * 2f) + p3.Y);
        var b = new Vector2(p12.X - p12.Z, p12.Y - p12.W);
        var linear = MathF.Abs(a.X) < NearlyLinear;
        var ra = 1f / (linear ? 1f : a.X);
        var rb = 0.5f / (b.X == 0f ? 1f : b.X);
        var d = MathF.Sqrt(MathF.Max((b.X * b.X) - (a.X * p12.X), 0f));
        var t1 = linear ? p12.X * rb : (b.X - d) * ra;
        var t2 = linear ? p12.X * rb : (b.X + d) * ra;
        return new Vector2(
            (((a.Y * t1) - (b.Y * 2f)) * t1) + p12.Y,
            (((a.Y * t2) - (b.Y * 2f)) * t2) + p12.Y);
    }

    private static float Saturate(float value) => Math.Clamp(value, 0f, 1f);
}
