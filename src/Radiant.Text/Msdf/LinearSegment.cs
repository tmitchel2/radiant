// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/edge-segments.cpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>A straight edge.</summary>
internal sealed class LinearSegment(Vector2d p0, Vector2d p1, EdgeColor color = EdgeColor.White) : EdgeSegment(color)
{
    public Vector2d P0 = p0;
    public Vector2d P1 = p1;

    public override int Type => 1;

    public override void CopyControlPoints(Span<Vector2d> points)
    {
        points[0] = P0;
        points[1] = P1;
    }

    public override Vector2d Point(double param) => Vector2d.Mix(P0, P1, param);

    public override Vector2d Direction(double param) => P1 - P0;

    public override SignedDistance GetSignedDistance(Vector2d origin, out double param)
    {
        var aq = origin - P0;
        var ab = P1 - P0;
        param = Vector2d.Dot(aq, ab) / Vector2d.Dot(ab, ab);
        var eq = (param > .5 ? P1 : P0) - origin;
        var endpointDistance = eq.Length;
        if (param > 0 && param < 1)
        {
            var orthoDistance = Vector2d.Dot(ab.GetOrthonormal(false), aq);
            if (Math.Abs(orthoDistance) < endpointDistance)
            {
                return new SignedDistance(orthoDistance, 0);
            }
        }
        return new SignedDistance(
            NonZeroSign(Vector2d.Cross(aq, ab)) * endpointDistance,
            Math.Abs(Vector2d.Dot(ab.Normalize(), eq.Normalize())));
    }

    public override int ScanlineIntersections(Span<double> x, Span<int> dy, double y)
    {
        if ((y >= P0.Y && y < P1.Y) || (y >= P1.Y && y < P0.Y))
        {
            var param = (y - P0.Y) / (P1.Y - P0.Y);
            x[0] = (1 - param) * P0.X + param * P1.X;
            dy[0] = Math.Sign(P1.Y - P0.Y);
            return 1;
        }
        return 0;
    }

    public override void Bound(ref double xMin, ref double yMin, ref double xMax, ref double yMax)
    {
        PointBounds(P0, ref xMin, ref yMin, ref xMax, ref yMax);
        PointBounds(P1, ref xMin, ref yMin, ref xMax, ref yMax);
    }

    public override void Reverse() => (P0, P1) = (P1, P0);

    public override (EdgeSegment Part0, EdgeSegment Part1, EdgeSegment Part2) SplitInThirds() => (
        new LinearSegment(P0, Point(1 / 3.0), Color),
        new LinearSegment(Point(1 / 3.0), Point(2 / 3.0), Color),
        new LinearSegment(Point(2 / 3.0), P1, Color));
}
