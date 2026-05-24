using System;
using System.Numerics;

namespace Radiant.MsdfBaker
{
    public sealed class QuadraticSegment : EdgeSegment
    {
        public Vector2 P0 { get; }
        public Vector2 P1 { get; }
        public Vector2 P2 { get; }

        public QuadraticSegment(Vector2 p0, Vector2 p1, Vector2 p2, EdgeColor color = EdgeColor.White)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            Color = color;
        }

        public override Vector2 Point(double t)
        {
            var a = Geometry.Mix(P0, P1, t);
            var b = Geometry.Mix(P1, P2, t);
            return Geometry.Mix(a, b, t);
        }

        public override Vector2 Direction(double t) => Geometry.Mix(P1 - P0, P2 - P1, t);

        public override SignedDistance SignedDistance(Vector2 origin, out double t)
        {
            var qa = P0 - origin;
            var ab = P1 - P0;
            var br = P2 - P1 - ab;

            var a = (double)Geometry.DotD(br, br);
            var b = 3 * Geometry.DotD(ab, br);
            var c = 2 * Geometry.DotD(ab, ab) + Geometry.DotD(qa, br);
            var d = Geometry.DotD(qa, ab);

            Span<double> roots = stackalloc double[3];
            var solutions = Geometry.SolveCubic(roots, a, b, c, d);

            var minDistance = Geometry.NonZeroSign(Geometry.Cross(ab, qa)) * Geometry.LengthD(qa);
            t = -Geometry.DotD(qa, ab) / Geometry.DotD(ab, ab);
            {
                var d1End = Geometry.NonZeroSign(Geometry.Cross(P2 - P1, P2 - origin)) * Geometry.LengthD(P2 - origin);
                if (Math.Abs(d1End) < Math.Abs(minDistance))
                {
                    minDistance = d1End;
                    t = Geometry.DotD(origin - P1, P2 - P1) / Geometry.DotD(P2 - P1, P2 - P1);
                }
            }
            for (var i = 0; i < solutions; i++)
            {
                if (roots[i] > 0 && roots[i] < 1)
                {
                    var qe = P0 + (Vector2)(2 * (float)roots[i] * ab) + (Vector2)((float)(roots[i] * roots[i]) * br) - origin;
                    var dist = Geometry.NonZeroSign(Geometry.Cross(ab + (Vector2)((float)roots[i] * br), qe)) * Geometry.LengthD(qe);
                    if (Math.Abs(dist) <= Math.Abs(minDistance))
                    {
                        minDistance = dist;
                        t = roots[i];
                    }
                }
            }

            if (t >= 0 && t <= 1)
            {
                return new SignedDistance(minDistance, 0);
            }
            var tangent = Geometry.NormalizeSafe(t < 0.5 ? ab : P2 - P1);
            var clampDir = Geometry.NormalizeSafe(t < 0.5 ? qa : P2 - origin);
            return new SignedDistance(minDistance, Math.Abs(Geometry.DotD(tangent, clampDir)));
        }
    }
}
