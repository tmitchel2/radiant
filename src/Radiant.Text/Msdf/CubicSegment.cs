// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/edge-segments.cpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>
/// A cubic Bézier edge, as CFF outlines are made of. The nearest point is found by Newton
/// iteration from several starting points along the curve.
/// </summary>
internal sealed class CubicSegment(Vector2d p0, Vector2d p1, Vector2d p2, Vector2d p3, EdgeColor color = EdgeColor.White) : EdgeSegment(color)
{
    // Parameters for iterative search of closest point on a cubic Bezier curve. Increase for higher precision.
    private const int SearchStarts = 4;
    private const int SearchSteps = 4;

    public Vector2d P0 = p0;
    public Vector2d P1 = p1;
    public Vector2d P2 = p2;
    public Vector2d P3 = p3;

    public override int Type => 3;

    public override void CopyControlPoints(Span<Vector2d> points)
    {
        points[0] = P0;
        points[1] = P1;
        points[2] = P2;
        points[3] = P3;
    }

    public override Vector2d Point(double param)
    {
        var p12 = Vector2d.Mix(P1, P2, param);
        return Vector2d.Mix(
            Vector2d.Mix(Vector2d.Mix(P0, P1, param), p12, param),
            Vector2d.Mix(p12, Vector2d.Mix(P2, P3, param), param),
            param);
    }

    public override Vector2d Direction(double param)
    {
        var tangent = Vector2d.Mix(Vector2d.Mix(P1 - P0, P2 - P1, param), Vector2d.Mix(P2 - P1, P3 - P2, param), param);
        if (!tangent.IsNonZero)
        {
            if (param == 0)
            {
                return P2 - P0;
            }
            if (param == 1)
            {
                return P3 - P1;
            }
        }
        return tangent;
    }

    public override SignedDistance GetSignedDistance(Vector2d origin, out double param)
    {
        var qa = P0 - origin;
        var ab = P1 - P0;
        var br = P2 - P1 - ab;
        var @as = P3 - P2 - (P2 - P1) - br;

        var epDir = Direction(0);
        var minDistance = NonZeroSign(Vector2d.Cross(epDir, qa)) * qa.Length; // distance from A
        param = -Vector2d.Dot(qa, epDir) / Vector2d.Dot(epDir, epDir);
        {
            var distance = (P3 - origin).Length; // distance from B
            if (distance < Math.Abs(minDistance))
            {
                epDir = Direction(1);
                minDistance = NonZeroSign(Vector2d.Cross(epDir, P3 - origin)) * distance;
                param = Vector2d.Dot(epDir - (P3 - origin), epDir) / Vector2d.Dot(epDir, epDir);
            }
        }
        // Iterative minimum distance search
        for (var i = 0; i <= SearchStarts; ++i)
        {
            var t = 1.0 / SearchStarts * i;
            var qe = qa + 3 * t * ab + 3 * t * t * br + t * t * t * @as;
            var d1 = 3 * ab + 6 * t * br + 3 * t * t * @as;
            var d2 = 6 * br + 6 * t * @as;
            var improvedT = t - Vector2d.Dot(qe, d1) / (Vector2d.Dot(d1, d1) + Vector2d.Dot(qe, d2));
            if (improvedT > 0 && improvedT < 1)
            {
                var remainingSteps = SearchSteps;
                do
                {
                    t = improvedT;
                    qe = qa + 3 * t * ab + 3 * t * t * br + t * t * t * @as;
                    d1 = 3 * ab + 6 * t * br + 3 * t * t * @as;
                    if (--remainingSteps == 0)
                    {
                        break;
                    }
                    d2 = 6 * br + 6 * t * @as;
                    improvedT = t - Vector2d.Dot(qe, d1) / (Vector2d.Dot(d1, d1) + Vector2d.Dot(qe, d2));
                }
                while (improvedT > 0 && improvedT < 1);
                var distance = qe.Length;
                if (distance < Math.Abs(minDistance))
                {
                    minDistance = NonZeroSign(Vector2d.Cross(d1, qe)) * distance;
                    param = t;
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
        return new SignedDistance(minDistance, Math.Abs(Vector2d.Dot(Direction(1).Normalize(), (P3 - origin).Normalize())));
    }

    public override int ScanlineIntersections(Span<double> x, Span<int> dy, double y)
    {
        var total = 0;
        var nextDy = y > P0.Y ? 1 : -1;
        x[total] = P0.X;
        if (P0.Y == y)
        {
            if (P0.Y < P1.Y || (P0.Y == P1.Y && (P0.Y < P2.Y || (P0.Y == P2.Y && P0.Y < P3.Y))))
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
            var @as = P3 - P2 - (P2 - P1) - br;
            Span<double> t = stackalloc double[3];
            var solutions = EquationSolver.SolveCubic(t, @as.Y, 3 * br.Y, 3 * ab.Y, P0.Y - y);
            // Sort solutions
            if (solutions >= 2)
            {
                if (t[0] > t[1])
                {
                    (t[0], t[1]) = (t[1], t[0]);
                }
                if (solutions >= 3 && t[1] > t[2])
                {
                    (t[1], t[2]) = (t[2], t[1]);
                    if (t[0] > t[1])
                    {
                        (t[0], t[1]) = (t[1], t[0]);
                    }
                }
            }
            for (var i = 0; i < solutions && total < 3; ++i)
            {
                if (t[i] >= 0 && t[i] <= 1)
                {
                    x[total] = P0.X + 3 * t[i] * ab.X + 3 * t[i] * t[i] * br.X + t[i] * t[i] * t[i] * @as.X;
                    if (nextDy * (ab.Y + 2 * t[i] * br.Y + t[i] * t[i] * @as.Y) >= 0)
                    {
                        dy[total++] = nextDy;
                        nextDy = -nextDy;
                    }
                }
            }
        }
        if (P3.Y == y)
        {
            if (nextDy > 0 && total > 0)
            {
                --total;
                nextDy = -1;
            }
            if ((P3.Y < P2.Y || (P3.Y == P2.Y && (P3.Y < P1.Y || (P3.Y == P1.Y && P3.Y < P0.Y)))) && total < 3)
            {
                x[total] = P3.X;
                if (nextDy < 0)
                {
                    dy[total++] = -1;
                    nextDy = 1;
                }
            }
        }
        if (nextDy != (y >= P3.Y ? 1 : -1))
        {
            if (total > 0)
            {
                --total;
            }
            else
            {
                if (Math.Abs(P3.Y - y) < Math.Abs(P0.Y - y))
                {
                    x[total] = P3.X;
                }
                dy[total++] = nextDy;
            }
        }
        return total;
    }

    public override void Bound(ref double xMin, ref double yMin, ref double xMax, ref double yMax)
    {
        PointBounds(P0, ref xMin, ref yMin, ref xMax, ref yMax);
        PointBounds(P3, ref xMin, ref yMin, ref xMax, ref yMax);
        var a0 = P1 - P0;
        var a1 = 2 * (P2 - P1 - a0);
        var a2 = P3 - 3 * P2 + 3 * P1 - P0;
        Span<double> parameters = stackalloc double[2];
        var solutions = EquationSolver.SolveQuadratic(parameters, a2.X, a1.X, a0.X);
        for (var i = 0; i < solutions; ++i)
        {
            if (parameters[i] > 0 && parameters[i] < 1)
            {
                PointBounds(Point(parameters[i]), ref xMin, ref yMin, ref xMax, ref yMax);
            }
        }
        solutions = EquationSolver.SolveQuadratic(parameters, a2.Y, a1.Y, a0.Y);
        for (var i = 0; i < solutions; ++i)
        {
            if (parameters[i] > 0 && parameters[i] < 1)
            {
                PointBounds(Point(parameters[i]), ref xMin, ref yMin, ref xMax, ref yMax);
            }
        }
    }

    public override void Reverse()
    {
        (P0, P3) = (P3, P0);
        (P1, P2) = (P2, P1);
    }

    public override (EdgeSegment Part0, EdgeSegment Part1, EdgeSegment Part2) SplitInThirds() => (
        new CubicSegment(
            P0,
            P0 == P1 ? P0 : Vector2d.Mix(P0, P1, 1 / 3.0),
            Vector2d.Mix(Vector2d.Mix(P0, P1, 1 / 3.0), Vector2d.Mix(P1, P2, 1 / 3.0), 1 / 3.0),
            Point(1 / 3.0),
            Color),
        new CubicSegment(
            Point(1 / 3.0),
            Vector2d.Mix(
                Vector2d.Mix(Vector2d.Mix(P0, P1, 1 / 3.0), Vector2d.Mix(P1, P2, 1 / 3.0), 1 / 3.0),
                Vector2d.Mix(Vector2d.Mix(P1, P2, 1 / 3.0), Vector2d.Mix(P2, P3, 1 / 3.0), 1 / 3.0),
                2 / 3.0),
            Vector2d.Mix(
                Vector2d.Mix(Vector2d.Mix(P0, P1, 2 / 3.0), Vector2d.Mix(P1, P2, 2 / 3.0), 2 / 3.0),
                Vector2d.Mix(Vector2d.Mix(P1, P2, 2 / 3.0), Vector2d.Mix(P2, P3, 2 / 3.0), 2 / 3.0),
                1 / 3.0),
            Point(2 / 3.0),
            Color),
        new CubicSegment(
            Point(2 / 3.0),
            Vector2d.Mix(Vector2d.Mix(P1, P2, 2 / 3.0), Vector2d.Mix(P2, P3, 2 / 3.0), 2 / 3.0),
            P2 == P3 ? P3 : Vector2d.Mix(P2, P3, 2 / 3.0),
            P3,
            Color));
}
