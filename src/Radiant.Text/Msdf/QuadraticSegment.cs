// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/edge-segments.cpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>
/// A quadratic Bézier edge, as TrueType outlines are made of. The nearest point to a given point
/// is a root of a cubic, so its distance is exact.
/// </summary>
internal sealed class QuadraticSegment(Vector2d p0, Vector2d p1, Vector2d p2, EdgeColor color = EdgeColor.White) : EdgeSegment(color)
{
    public Vector2d P0 = p0;
    public Vector2d P1 = p1;
    public Vector2d P2 = p2;

    public override int Type => 2;

    public override void CopyControlPoints(Span<Vector2d> points)
    {
        points[0] = P0;
        points[1] = P1;
        points[2] = P2;
    }

    public override Vector2d Point(double param) => Vector2d.Mix(Vector2d.Mix(P0, P1, param), Vector2d.Mix(P1, P2, param), param);

    public override Vector2d Direction(double param)
    {
        var tangent = Vector2d.Mix(P1 - P0, P2 - P1, param);
        return tangent.IsNonZero ? tangent : P2 - P0;
    }

    public override SignedDistance GetSignedDistance(Vector2d origin, out double param)
    {
        var qa = P0 - origin;
        var ab = P1 - P0;
        var br = P2 - P1 - ab;
        var a = Vector2d.Dot(br, br);
        var b = 3 * Vector2d.Dot(ab, br);
        var c = 2 * Vector2d.Dot(ab, ab) + Vector2d.Dot(qa, br);
        var d = Vector2d.Dot(qa, ab);
        Span<double> t = stackalloc double[3];
        var solutions = EquationSolver.SolveCubic(t, a, b, c, d);

        var epDir = Direction(0);
        var minDistance = NonZeroSign(Vector2d.Cross(epDir, qa)) * qa.Length; // distance from A
        param = -Vector2d.Dot(qa, epDir) / Vector2d.Dot(epDir, epDir);
        {
            var distance = (P2 - origin).Length; // distance from B
            if (distance < Math.Abs(minDistance))
            {
                epDir = Direction(1);
                minDistance = NonZeroSign(Vector2d.Cross(epDir, P2 - origin)) * distance;
                param = Vector2d.Dot(origin - P1, epDir) / Vector2d.Dot(epDir, epDir);
            }
        }
        for (var i = 0; i < solutions; ++i)
        {
            if (t[i] > 0 && t[i] < 1)
            {
                var qe = qa + 2 * t[i] * ab + t[i] * t[i] * br;
                var distance = qe.Length;
                if (distance <= Math.Abs(minDistance))
                {
                    minDistance = NonZeroSign(Vector2d.Cross(ab + t[i] * br, qe)) * distance;
                    param = t[i];
                }
            }
        }

        if (param >= 0 && param <= 1)
        {
            return new SignedDistance(minDistance, 0);
        }
        if (param < .5)
        {
            return new SignedDistance(minDistance, Math.Abs(Vector2d.Dot(Direction(0).Normalize(), qa.Normalize())));
        }
        return new SignedDistance(minDistance, Math.Abs(Vector2d.Dot(Direction(1).Normalize(), (P2 - origin).Normalize())));
    }

    public override int ScanlineIntersections(Span<double> x, Span<int> dy, double y)
    {
        var total = 0;
        var nextDy = y > P0.Y ? 1 : -1;
        x[total] = P0.X;
        if (P0.Y == y)
        {
            if (P0.Y < P1.Y || (P0.Y == P1.Y && P0.Y < P2.Y))
            {
                dy[total++] = 1;
            }
            else
            {
                nextDy = 1;
            }
        }
        {
            var ab = P1 - P0;
            var br = P2 - P1 - ab;
            Span<double> t = stackalloc double[2];
            var solutions = EquationSolver.SolveQuadratic(t, br.Y, 2 * ab.Y, P0.Y - y);
            // Sort solutions
            if (solutions >= 2 && t[0] > t[1])
            {
                (t[0], t[1]) = (t[1], t[0]);
            }
            for (var i = 0; i < solutions && total < 2; ++i)
            {
                if (t[i] >= 0 && t[i] <= 1)
                {
                    x[total] = P0.X + 2 * t[i] * ab.X + t[i] * t[i] * br.X;
                    if (nextDy * (ab.Y + t[i] * br.Y) >= 0)
                    {
                        dy[total++] = nextDy;
                        nextDy = -nextDy;
                    }
                }
            }
        }
        if (P2.Y == y)
        {
            if (nextDy > 0 && total > 0)
            {
                --total;
                nextDy = -1;
            }
            if ((P2.Y < P1.Y || (P2.Y == P1.Y && P2.Y < P0.Y)) && total < 2)
            {
                x[total] = P2.X;
                if (nextDy < 0)
                {
                    dy[total++] = -1;
                    nextDy = 1;
                }
            }
        }
        if (nextDy != (y >= P2.Y ? 1 : -1))
        {
            if (total > 0)
            {
                --total;
            }
            else
            {
                if (Math.Abs(P2.Y - y) < Math.Abs(P0.Y - y))
                {
                    x[total] = P2.X;
                }
                dy[total++] = nextDy;
            }
        }
        return total;
    }

    public override void Bound(ref double xMin, ref double yMin, ref double xMax, ref double yMax)
    {
        PointBounds(P0, ref xMin, ref yMin, ref xMax, ref yMax);
        PointBounds(P2, ref xMin, ref yMin, ref xMax, ref yMax);
        var bot = P1 - P0 - (P2 - P1);
        if (bot.X != 0)
        {
            var param = (P1.X - P0.X) / bot.X;
            if (param > 0 && param < 1)
            {
                PointBounds(Point(param), ref xMin, ref yMin, ref xMax, ref yMax);
            }
        }
        if (bot.Y != 0)
        {
            var param = (P1.Y - P0.Y) / bot.Y;
            if (param > 0 && param < 1)
            {
                PointBounds(Point(param), ref xMin, ref yMin, ref xMax, ref yMax);
            }
        }
    }

    public override void Reverse() => (P0, P2) = (P2, P0);

    public override (EdgeSegment Part0, EdgeSegment Part1, EdgeSegment Part2) SplitInThirds() => (
        new QuadraticSegment(P0, Vector2d.Mix(P0, P1, 1 / 3.0), Point(1 / 3.0), Color),
        new QuadraticSegment(Point(1 / 3.0), Vector2d.Mix(Vector2d.Mix(P0, P1, 5 / 9.0), Vector2d.Mix(P1, P2, 4 / 9.0), .5), Point(2 / 3.0), Color),
        new QuadraticSegment(Point(2 / 3.0), Vector2d.Mix(P1, P2, 2 / 3.0), P2, Color));

    /// <summary>The same curve as a cubic, whose control points can be moved independently.</summary>
    public CubicSegment ConvertToCubic() =>
        new(P0, Vector2d.Mix(P0, P1, 2 / 3.0), Vector2d.Mix(P1, P2, 1 / 3.0), P2, Color);
}
