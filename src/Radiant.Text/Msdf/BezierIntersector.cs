using System;
using System.Collections.Generic;

namespace Radiant.Text.Msdf;

/// <summary>
/// Where two edges cross, and pieces of an edge between two parameters, for resolving
/// crossing contours (<see cref="OverlapResolver"/>). This is Radiant's, not msdfgen's:
/// msdfgen leaves such geometry to Skia's path simplification.
/// <para>
/// Crossings are found by subdividing both curves (de Casteljau at the midpoint) wherever their
/// control-point boxes overlap, until both pieces are flat to within a tolerance, and then
/// intersecting the pieces' chords. That works the same for lines, quadratics and cubics, and
/// finds every transversal crossing.
/// </para>
/// </summary>
internal static class BezierIntersector
{
    private const int MaxDepth = 48;

    /// <summary>
    /// Adds to <paramref name="results"/> the parameters (on <paramref name="a"/>, on <paramref name="b"/>)
    /// at which the two edges cross.
    /// </summary>
    /// <param name="tolerance">How far from its chord a piece may be to count as flat, in shape units.</param>
    public static void Intersect(EdgeSegment a, EdgeSegment b, double tolerance, List<(double Ta, double Tb)> results)
    {
        Span<Vector2d> pa = stackalloc Vector2d[4];
        Span<Vector2d> pb = stackalloc Vector2d[4];
        a.CopyControlPoints(pa);
        b.CopyControlPoints(pb);
        Intersect(pa[..(a.Type + 1)], 0, 1, pb[..(b.Type + 1)], 0, 1, tolerance, 0, results);
    }

    /// <summary>The control points of the piece of a curve between parameters <paramref name="t0"/> and <paramref name="t1"/>.</summary>
    public static void Section(ReadOnlySpan<Vector2d> points, double t0, double t1, Span<Vector2d> section)
    {
        var degree = points.Length - 1;
        Span<Vector2d> left = stackalloc Vector2d[4];
        Span<Vector2d> right = stackalloc Vector2d[4];
        points.CopyTo(section);
        if (t1 < 1)
        {
            Split(section[..(degree + 1)], t1, left, right);
            left[..(degree + 1)].CopyTo(section);
        }
        if (t0 > 0)
        {
            Split(section[..(degree + 1)], t1 > 0 ? t0 / t1 : 0, left, right);
            right[..(degree + 1)].CopyTo(section);
        }
    }

    private static void Intersect(
        ReadOnlySpan<Vector2d> a, double a0, double a1, ReadOnlySpan<Vector2d> b, double b0, double b1,
        double tolerance, int depth, List<(double Ta, double Tb)> results)
    {
        if (!BoxesOverlap(a, b, tolerance))
        {
            return;
        }
        var aFlat = IsFlat(a, tolerance);
        var bFlat = IsFlat(b, tolerance);
        if ((aFlat && bFlat) || depth >= MaxDepth)
        {
            if (ChordIntersection(a[0], a[^1], b[0], b[^1], out var s, out var u))
            {
                results.Add((a0 + s * (a1 - a0), b0 + u * (b1 - b0)));
            }
            return;
        }
        Span<Vector2d> left = stackalloc Vector2d[4];
        Span<Vector2d> right = stackalloc Vector2d[4];
        // Subdivide the piece that is not flat, or the larger if neither is.
        if (!aFlat && (bFlat || Extent(a) >= Extent(b)))
        {
            Split(a, .5, left, right);
            var am = .5 * (a0 + a1);
            Intersect(left[..a.Length], a0, am, b, b0, b1, tolerance, depth + 1, results);
            Intersect(right[..a.Length], am, a1, b, b0, b1, tolerance, depth + 1, results);
        }
        else
        {
            Split(b, .5, left, right);
            var bm = .5 * (b0 + b1);
            Intersect(a, a0, a1, left[..b.Length], b0, bm, tolerance, depth + 1, results);
            Intersect(a, a0, a1, right[..b.Length], bm, b1, tolerance, depth + 1, results);
        }
    }

    /// <summary>De Casteljau: the curve split at <paramref name="t"/> into two of the same degree.</summary>
    private static void Split(ReadOnlySpan<Vector2d> p, double t, Span<Vector2d> left, Span<Vector2d> right)
    {
        var n = p.Length;
        Span<Vector2d> work = stackalloc Vector2d[4];
        p.CopyTo(work);
        left[0] = work[0];
        right[n - 1] = work[n - 1];
        for (var level = 1; level < n; level++)
        {
            for (var i = 0; i < n - level; i++)
            {
                work[i] = Vector2d.Mix(work[i], work[i + 1], t);
            }
            left[level] = work[0];
            right[n - 1 - level] = work[n - 1 - level];
        }
    }

    private static bool BoxesOverlap(ReadOnlySpan<Vector2d> a, ReadOnlySpan<Vector2d> b, double margin)
    {
        var (aMin, aMax) = Box(a);
        var (bMin, bMax) = Box(b);
        return aMin.X <= bMax.X + margin && bMin.X <= aMax.X + margin && aMin.Y <= bMax.Y + margin && bMin.Y <= aMax.Y + margin;
    }

    private static (Vector2d Min, Vector2d Max) Box(ReadOnlySpan<Vector2d> p)
    {
        double minX = p[0].X, minY = p[0].Y, maxX = minX, maxY = minY;
        for (var i = 1; i < p.Length; i++)
        {
            minX = Math.Min(minX, p[i].X);
            minY = Math.Min(minY, p[i].Y);
            maxX = Math.Max(maxX, p[i].X);
            maxY = Math.Max(maxY, p[i].Y);
        }
        return (new Vector2d(minX, minY), new Vector2d(maxX, maxY));
    }

    private static double Extent(ReadOnlySpan<Vector2d> p)
    {
        var (min, max) = Box(p);
        return Math.Max(max.X - min.X, max.Y - min.Y);
    }

    /// <summary>Whether every control point is within <paramref name="tolerance"/> of the chord.</summary>
    private static bool IsFlat(ReadOnlySpan<Vector2d> p, double tolerance)
    {
        if (p.Length == 2)
        {
            return true;
        }
        var chord = p[^1] - p[0];
        var length = chord.Length;
        for (var i = 1; i < p.Length - 1; i++)
        {
            var offset = p[i] - p[0];
            var distance = length > 0 ? Math.Abs(Vector2d.Cross(chord, offset)) / length : offset.Length;
            if (distance > tolerance)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Where segments p0–p1 and q0–q1 cross, as parameters along each; false if parallel or apart.</summary>
    private static bool ChordIntersection(Vector2d p0, Vector2d p1, Vector2d q0, Vector2d q1, out double s, out double u)
    {
        var r = p1 - p0;
        var d = q1 - q0;
        var denominator = Vector2d.Cross(r, d);
        s = u = 0;
        if (denominator == 0)
        {
            return false;
        }
        var qp = q0 - p0;
        s = Vector2d.Cross(qp, d) / denominator;
        u = Vector2d.Cross(qp, r) / denominator;
        // The ends are included: a crossing exactly at a subdivision point is found by both halves
        // and merged by the caller.
        return s >= 0 && s <= 1 && u >= 0 && u <= 1;
    }
}
