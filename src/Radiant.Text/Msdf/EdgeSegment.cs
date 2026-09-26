// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/edge-segments.h and .cpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>
/// One edge of a contour: a line, or a quadratic or cubic Bézier. Each kind answers the questions
/// the distance field asks of an edge: how far a point is from it (signed, so the side of the
/// edge the point is on is known), which way it runs, and where it ends.
/// </summary>
internal abstract class EdgeSegment(EdgeColor color)
{
    /// <summary>The channels this edge contributes its distance to.</summary>
    public EdgeColor Color { get; set; } = color;

    /// <summary>The degree: 1 for a line, 2 for a quadratic, 3 for a cubic.</summary>
    public abstract int Type { get; }

    /// <summary>Makes a line.</summary>
    public static EdgeSegment Create(Vector2d p0, Vector2d p1, EdgeColor color = EdgeColor.White) =>
        new LinearSegment(p0, p1, color);

    /// <summary>Makes a quadratic, or a line if its control point is in line with its ends.</summary>
    public static EdgeSegment Create(Vector2d p0, Vector2d p1, Vector2d p2, EdgeColor color = EdgeColor.White)
    {
        if (Vector2d.Cross(p1 - p0, p2 - p1) == 0)
        {
            return new LinearSegment(p0, p2, color);
        }
        return new QuadraticSegment(p0, p1, p2, color);
    }

    /// <summary>Makes a cubic, or the line or quadratic it really is.</summary>
    public static EdgeSegment Create(Vector2d p0, Vector2d p1, Vector2d p2, Vector2d p3, EdgeColor color = EdgeColor.White)
    {
        var p12 = p2 - p1;
        if (Vector2d.Cross(p1 - p0, p12) == 0 && Vector2d.Cross(p12, p3 - p2) == 0)
        {
            return new LinearSegment(p0, p3, color);
        }
        if ((p12 = 1.5 * p1 - .5 * p0) == 1.5 * p2 - .5 * p3)
        {
            return new QuadraticSegment(p0, p12, p3, color);
        }
        return new CubicSegment(p0, p1, p2, p3, color);
    }

    /// <summary>Copies the control points (<see cref="Type"/> + 1 of them) into <paramref name="points"/>.</summary>
    public abstract void CopyControlPoints(Span<Vector2d> points);

    /// <summary>The point at <paramref name="param"/>, from 0 at the start to 1 at the end.</summary>
    public abstract Vector2d Point(double param);

    /// <summary>The direction the edge runs at <paramref name="param"/> (not normalized).</summary>
    public abstract Vector2d Direction(double param);

    /// <summary>The minimum signed distance from <paramref name="origin"/> to the edge, and where on it that is.</summary>
    public abstract SignedDistance GetSignedDistance(Vector2d origin, out double param);

    /// <summary>
    /// Converts a distance found by <see cref="GetSignedDistance"/> into the distance to the edge
    /// extended past its end along its end direction, when the nearest point was past an end. This
    /// is the pseudo-distance that keeps the channels straight up to a corner.
    /// </summary>
    public void DistanceToPerpendicularDistance(ref SignedDistance distance, Vector2d origin, double param)
    {
        if (param < 0)
        {
            var dir = Direction(0).Normalize();
            var aq = origin - Point(0);
            var ts = Vector2d.Dot(aq, dir);
            if (ts < 0)
            {
                var perpendicularDistance = Vector2d.Cross(aq, dir);
                if (Math.Abs(perpendicularDistance) <= Math.Abs(distance.Distance))
                {
                    distance = new SignedDistance(perpendicularDistance, 0);
                }
            }
        }
        else if (param > 1)
        {
            var dir = Direction(1).Normalize();
            var bq = origin - Point(1);
            var ts = Vector2d.Dot(bq, dir);
            if (ts > 0)
            {
                var perpendicularDistance = Vector2d.Cross(bq, dir);
                if (Math.Abs(perpendicularDistance) <= Math.Abs(distance.Distance))
                {
                    distance = new SignedDistance(perpendicularDistance, 0);
                }
            }
        }
    }

    /// <summary>
    /// Where the edge crosses the horizontal line at <paramref name="y"/>: up to three x
    /// coordinates, each with the direction the edge crosses in (1 up, -1 down). Ends exactly on
    /// the line are counted so that a contour's crossings always add up consistently.
    /// </summary>
    /// <returns>The number of crossings.</returns>
    public abstract int ScanlineIntersections(Span<double> x, Span<int> dy, double y);

    /// <summary>Grows a bounding box to fit the edge.</summary>
    public abstract void Bound(ref double xMin, ref double yMin, ref double xMax, ref double yMax);

    /// <summary>Swaps the start and end.</summary>
    public abstract void Reverse();

    /// <summary>Three edges that together are this one, for contours too short to colour otherwise.</summary>
    public abstract (EdgeSegment Part0, EdgeSegment Part1, EdgeSegment Part2) SplitInThirds();

    /// <summary>1 for positive values, -1 otherwise (msdfgen's <c>nonZeroSign</c>).</summary>
    protected static int NonZeroSign(double n) => n > 0 ? 1 : -1;

    protected static void PointBounds(Vector2d p, ref double xMin, ref double yMin, ref double xMax, ref double yMax)
    {
        if (p.X < xMin)
        {
            xMin = p.X;
        }
        if (p.Y < yMin)
        {
            yMin = p.Y;
        }
        if (p.X > xMax)
        {
            xMax = p.X;
        }
        if (p.Y > yMax)
        {
            yMax = p.Y;
        }
    }
}
