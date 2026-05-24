using System;
using System.Numerics;

namespace Radiant.MsdfBaker
{
    public sealed class CubicSegment : EdgeSegment
    {
        public Vector2 P0 { get; }
        public Vector2 P1 { get; }
        public Vector2 P2 { get; }
        public Vector2 P3 { get; }

        public CubicSegment(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, EdgeColor color = EdgeColor.White)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            P3 = p3;
            Color = color;
        }

        public override Vector2 Point(double t)
        {
            var p12 = Geometry.Mix(P1, P2, t);
            var a = Geometry.Mix(Geometry.Mix(P0, P1, t), p12, t);
            var b = Geometry.Mix(p12, Geometry.Mix(P2, P3, t), t);
            return Geometry.Mix(a, b, t);
        }

        public override Vector2 Direction(double t)
        {
            var tangent = Geometry.Mix(Geometry.Mix(P1 - P0, P2 - P1, t), Geometry.Mix(P2 - P1, P3 - P2, t), t);
            if (tangent == Vector2.Zero)
            {
                if (t == 0) return P2 - P0;
                if (t == 1) return P3 - P1;
            }
            return tangent;
        }

        public override SignedDistance SignedDistance(Vector2 origin, out double t)
        {
            var qa = P0 - origin;
            var ab = P1 - P0;
            var br = P2 - P1 - ab;
            var asV = (P3 - P2) - (P2 - P1) - br;

            var initialT = Geometry.DotD(P0 - origin, ab) >= 0 ? 0.0 : 1.0;
            var minDistance = Geometry.NonZeroSign(Geometry.Cross(ab, qa)) * Geometry.LengthD(qa);
            t = -Geometry.DotD(qa, ab) / Geometry.DotD(ab, ab);
            {
                var d1End = Geometry.NonZeroSign(Geometry.Cross(P3 - P2, P3 - origin)) * Geometry.LengthD(P3 - origin);
                if (Math.Abs(d1End) < Math.Abs(minDistance))
                {
                    minDistance = d1End;
                    t = Geometry.DotD(origin - P2, P3 - P2) / Geometry.DotD(P3 - P2, P3 - P2);
                }
            }

            const int searchStarts = 4;
            const int searchSteps = 4;
            for (var i = 0; i <= searchStarts; i++)
            {
                var ti = (double)i / searchStarts;
                for (var step = 0; step < searchSteps; step++)
                {
                    var qe = qa
                        + (Vector2)((float)(3 * ti) * ab)
                        + (Vector2)((float)(3 * ti * ti) * br)
                        + (Vector2)((float)(ti * ti * ti) * asV);
                    var d1 = ab
                        + (Vector2)((float)(2 * ti) * br)
                        + (Vector2)((float)(ti * ti) * asV);
                    d1 = new Vector2((float)(3 * d1.X), (float)(3 * d1.Y));
                    var d2 = br + (Vector2)((float)ti * asV);
                    d2 = new Vector2((float)(6 * d2.X), (float)(6 * d2.Y));
                    var num = Geometry.DotD(qe, d1);
                    var den = Geometry.DotD(d1, d1) + Geometry.DotD(qe, d2);
                    if (Math.Abs(den) < 1e-14) break;
                    var delta = num / den;
                    ti -= delta;
                    if (ti < 0 || ti > 1) break;
                    if (Math.Abs(delta) < 1e-7) break;
                }
                if (ti > 0 && ti < 1)
                {
                    var qe = qa
                        + (Vector2)((float)(3 * ti) * ab)
                        + (Vector2)((float)(3 * ti * ti) * br)
                        + (Vector2)((float)(ti * ti * ti) * asV);
                    var d1 = ab
                        + (Vector2)((float)(2 * ti) * br)
                        + (Vector2)((float)(ti * ti) * asV);
                    var dist = Geometry.NonZeroSign(Geometry.Cross(d1, qe)) * Geometry.LengthD(qe);
                    if (Math.Abs(dist) <= Math.Abs(minDistance))
                    {
                        minDistance = dist;
                        t = ti;
                    }
                }
            }

            if (t >= 0 && t <= 1)
            {
                return new SignedDistance(minDistance, 0);
            }
            var tangent = Geometry.NormalizeSafe(t < 0.5 ? ab : P3 - P2);
            var clampDir = Geometry.NormalizeSafe(t < 0.5 ? qa : P3 - origin);
            return new SignedDistance(minDistance, Math.Abs(Geometry.DotD(tangent, clampDir)));
        }
    }
}
