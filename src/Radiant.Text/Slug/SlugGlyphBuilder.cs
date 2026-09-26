using System;
using System.Collections.Generic;
using System.Numerics;

namespace Radiant.Text.Slug;

/// <summary>
/// Prepares glyph outlines for Slug: every segment becomes a quadratic, the glyph is cut into
/// horizontal and vertical bands, and each band lists the curves a ray inside it could cross,
/// sorted so the ray can stop early. See <see cref="SlugGlyph"/> for the layout.
/// <para>
/// Follows Eric Lengyel's JCGT 2017 paper and the notes with his reference shaders
/// (github.com/EricLengyel/Slug, MIT): lines are quadratics with the control point on the end
/// point; bands are equal slices, overlapping by 1/1024 em so a sample on a boundary finds its
/// curves in either band; lines parallel to a band's rays are left out of it, since they can never
/// cross them; and adjacent bands with the same curves share one list.
/// </para>
/// </summary>
public static class SlugGlyphBuilder
{
    /// <summary>How far a cubic's quadratic approximation may stray from it, in em units.</summary>
    public const float CubicTolerance = 1f / 4096f;

    /// <summary>The most bands in either direction, as in the paper.</summary>
    public const int MaxBands = 16;

    // How far bands overlap, in em units (the reference's advice).
    private const float BandOverlap = 1f / 1024f;

    // The most quadratics one cubic becomes, however tight the tolerance.
    private const int MaxCubicPieces = 32;

    /// <summary>Prepares an outline in font units (y up) from a face with <paramref name="unitsPerEm"/>.</summary>
    public static SlugGlyph Build(GlyphOutline outline, int unitsPerEm)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(unitsPerEm);
        var contours = ToQuadratics(outline, unitsPerEm);
        return Build(contours);
    }

    /// <summary>
    /// Prepares contours that are already quadratics, in em units. Each contour should be closed;
    /// its curves are best chained end to start, which lets them share texels.
    /// </summary>
    public static SlugGlyph Build(IReadOnlyList<IReadOnlyList<SlugCurve>> contours)
    {
        ArgumentNullException.ThrowIfNull(contours);

        // Pack the curves, remembering each curve's texel. A curve that ends where the next begins
        // shares its end texel with it; any other ends with a texel of its own.
        var texels = new List<Vector4>();
        var curves = new List<SlugCurve>();
        var locations = new List<int>();
        foreach (var contour in contours)
        {
            for (var i = 0; i < contour.Count; i++)
            {
                var curve = contour[i];
                locations.Add(texels.Count);
                curves.Add(curve);
                texels.Add(new Vector4(curve.P1, curve.P2.X, curve.P2.Y));
                if (i + 1 == contour.Count || contour[i + 1].P1 != curve.P3)
                {
                    texels.Add(new Vector4(curve.P3, 0f, 0f));
                }
            }
        }
        if (curves.Count == 0)
        {
            return SlugGlyph.Empty;
        }

        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var curve in curves)
        {
            min = Vector2.Min(min, curve.Min);
            max = Vector2.Max(max, curve.Max);
        }

        // Horizontal bands slice y and hold the curves horizontal rays can cross; vertical, x.
        var horizontal = MakeBands(curves, min.Y, max.Y, vertical: false);
        var vertical = MakeBands(curves, min.X, max.X, vertical: true);

        var bands = new List<uint>
        {
            (uint)horizontal.Count,
            (uint)vertical.Count,
            BitConverter.SingleToUInt32Bits(BandScale(vertical.Count, min.X, max.X)),
            BitConverter.SingleToUInt32Bits(BandScale(horizontal.Count, min.Y, max.Y)),
            BitConverter.SingleToUInt32Bits(-min.X * BandScale(vertical.Count, min.X, max.X)),
            BitConverter.SingleToUInt32Bits(-min.Y * BandScale(horizontal.Count, min.Y, max.Y)),
        };
        var headerStart = bands.Count;
        for (var i = 0; i < 2 * (horizontal.Count + vertical.Count); i++)
        {
            bands.Add(0);
        }
        var header = headerStart;
        List<int>? previous = null;
        var previousOffset = 0;
        foreach (var band in (IEnumerable<List<int>>)[.. horizontal, .. vertical])
        {
            if (previous is null || !SameCurves(previous, band))
            {
                previousOffset = bands.Count;
                foreach (var curve in band)
                {
                    bands.Add((uint)locations[curve]);
                }
            }
            bands[header++] = (uint)band.Count;
            bands[header++] = (uint)previousOffset;
            previous = band;
        }

        return new SlugGlyph([.. texels], [.. bands], curves.Count, min, max);
    }

    /// <summary>
    /// An outline's contours as chains of quadratics in em units: lines become quadratics with the
    /// control point on the end, cubics are split into quadratics within <see cref="CubicTolerance"/>,
    /// open contours are closed, and zero-length segments are dropped.
    /// </summary>
    public static List<List<SlugCurve>> ToQuadratics(GlyphOutline outline, float unitsPerEm)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(unitsPerEm);
        var contours = new List<List<SlugCurve>>();
        var points = outline.Points;
        var p = 0;
        List<SlugCurve>? contour = null;
        Vector2 start = default, current = default;

        // A segment with no contour open (a malformed outline) starts one where the pen is.
        List<SlugCurve> Open()
        {
            if (contour is null)
            {
                contour = [];
                start = current;
            }
            return contour;
        }

        void Line(Vector2 to)
        {
            if (to != current)
            {
                Open().Add(new SlugCurve(current, to, to));
            }
            current = to;
        }

        void Close()
        {
            if (contour is null)
            {
                return;
            }
            Line(start);
            if (contour.Count > 0)
            {
                contours.Add(contour);
            }
            contour = null;
        }

        foreach (var verb in outline.Verbs)
        {
            switch (verb)
            {
                case PathVerb.MoveTo:
                    Close();
                    contour = [];
                    start = current = points[p++] / unitsPerEm;
                    break;
                case PathVerb.LineTo:
                    Line(points[p++] / unitsPerEm);
                    break;
                case PathVerb.QuadTo:
                {
                    var control = points[p++] / unitsPerEm;
                    var end = points[p++] / unitsPerEm;
                    if (control == current && end == current)
                    {
                        break;
                    }
                    Open().Add(new SlugCurve(current, control, end));
                    current = end;
                    break;
                }
                case PathVerb.CubicTo:
                {
                    var c1 = points[p++] / unitsPerEm;
                    var c2 = points[p++] / unitsPerEm;
                    var end = points[p++] / unitsPerEm;
                    AddCubic(Open(), current, c1, c2, end, CubicTolerance);
                    current = end;
                    break;
                }
                case PathVerb.Close:
                    Close();
                    break;
            }
        }
        Close();
        return contours;
    }

    /// <summary>
    /// Appends quadratics approximating a cubic within <paramref name="tolerance"/>. A single
    /// quadratic through the cubic's ends, with control point (3 (c1 + c2) - p0 - p3) / 4, is off
    /// by at most √3 / 36 × |p3 - 3 c2 + 3 c1 - p0|; splitting the cubic into n equal pieces cuts
    /// that third difference by n³, so n = ∛(error / tolerance) pieces are enough.
    /// </summary>
    internal static void AddCubic(List<SlugCurve> curves, Vector2 p0, Vector2 c1, Vector2 c2, Vector2 p3, float tolerance)
    {
        var error = MathF.Sqrt(3f) / 36f * (p3 - (3f * c2) + (3f * c1) - p0).Length();
        var n = Math.Clamp((int)MathF.Ceiling(MathF.Cbrt(error / tolerance)), 1, MaxCubicPieces);
        var from = p0;
        var fromTangent = CubicDerivative(p0, c1, c2, p3, 0f);
        for (var i = 1; i <= n; i++)
        {
            var t = i / (float)n;
            var to = i == n ? p3 : CubicPoint(p0, c1, c2, p3, t);
            var toTangent = CubicDerivative(p0, c1, c2, p3, t);
            // The piece as a cubic: its inner control points from the tangents at its ends.
            var third = 1f / (3f * n);
            var q1 = from + (fromTangent * third);
            var q2 = to - (toTangent * third);
            var control = ((3f * (q1 + q2)) - from - to) / 4f;
            curves.Add(new SlugCurve(from, control, to));
            from = to;
            fromTangent = toTangent;
        }
    }

    private static Vector2 CubicPoint(Vector2 p0, Vector2 c1, Vector2 c2, Vector2 p3, float t)
    {
        var u = 1f - t;
        return (u * u * u * p0) + (3f * u * u * t * c1) + (3f * u * t * t * c2) + (t * t * t * p3);
    }

    private static Vector2 CubicDerivative(Vector2 p0, Vector2 c1, Vector2 c2, Vector2 p3, float t)
    {
        var u = 1f - t;
        return (3f * u * u * (c1 - p0)) + (6f * u * t * (c2 - c1)) + (3f * t * t * (p3 - c2));
    }

    private static float BandScale(int count, float min, float max) => max > min ? count / (max - min) : 0f;

    /// <summary>
    /// Cuts [<paramref name="min"/>, <paramref name="max"/>] into equal bands and lists the curves in
    /// each, sorted by greatest coordinate along the rays, largest first. The paper makes the band
    /// count proportional to the curve count, up to 16; within that, this takes the count that
    /// leaves the fewest curves in the fullest band (fewer bands on a tie), which is what bounds a
    /// pixel's work.
    /// </summary>
    private static List<List<int>> MakeBands(List<SlugCurve> curves, float min, float max, bool vertical)
    {
        // The curves a ray along this band's direction can cross (not those parallel to it), with
        // their extent across the bands and their greatest coordinate along the rays.
        var candidates = new List<int>();
        var spans = new List<(float Lo, float Hi)>();
        for (var i = 0; i < curves.Count; i++)
        {
            var c = curves[i];
            var (lo, hi) = vertical ? (c.Min.X, c.Max.X) : (c.Min.Y, c.Max.Y);
            if (lo != hi)
            {
                candidates.Add(i);
                spans.Add((lo, hi));
            }
        }

        var bestCount = 1;
        var bestMost = int.MaxValue;
        var limit = Math.Clamp(candidates.Count, 1, MaxBands);
        Span<int> starts = stackalloc int[MaxBands + 1];
        for (var count = 1; count <= limit; count++)
        {
            // Each curve adds one to the bands it reaches: a running sum over where spans start and end.
            starts.Clear();
            var thickness = (max - min) / count;
            foreach (var (lo, hi) in spans)
            {
                var first = Math.Clamp((int)MathF.Ceiling(((lo - BandOverlap - min) / thickness) - 1f), 0, count - 1);
                var last = Math.Clamp((int)MathF.Floor((hi + BandOverlap - min) / thickness), 0, count - 1);
                starts[first]++;
                starts[last + 1]--;
            }
            var most = 0;
            var inBand = 0;
            for (var band = 0; band < count; band++)
            {
                inBand += starts[band];
                most = Math.Max(most, inBand);
            }
            if (most < bestMost)
            {
                bestMost = most;
                bestCount = count;
            }
        }

        var bands = new List<List<int>>(bestCount);
        var size = (max - min) / bestCount;
        for (var band = 0; band < bestCount; band++)
        {
            var bandLo = min + (band * size) - BandOverlap;
            var bandHi = min + ((band + 1) * size) + BandOverlap;
            var members = new List<(int Curve, float Greatest)>();
            for (var i = 0; i < candidates.Count; i++)
            {
                if (spans[i].Hi >= bandLo && spans[i].Lo <= bandHi)
                {
                    var c = curves[candidates[i]];
                    members.Add((candidates[i], vertical ? c.Max.Y : c.Max.X));
                }
            }
            // Largest greatest-coordinate first; ties by index keep the order deterministic.
            members.Sort((a, b) =>
            {
                var order = b.Greatest.CompareTo(a.Greatest);
                return order != 0 ? order : a.Curve.CompareTo(b.Curve);
            });
            bands.Add(members.ConvertAll(m => m.Curve));
        }
        return bands;
    }

    private static bool SameCurves(List<int> a, List<int> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }
}
