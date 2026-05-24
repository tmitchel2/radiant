using System;
using System.Numerics;

namespace Radiant.MsdfBaker
{
    public sealed class LinearSegment : EdgeSegment
    {
        public Vector2 P0 { get; }
        public Vector2 P1 { get; }

        public LinearSegment(Vector2 p0, Vector2 p1, EdgeColor color = EdgeColor.White)
        {
            P0 = p0;
            P1 = p1;
            Color = color;
        }

        public override Vector2 Point(double t) => Geometry.Mix(P0, P1, t);

        public override Vector2 Direction(double t) => P1 - P0;

        public override SignedDistance SignedDistance(Vector2 origin, out double t)
        {
            var aq = origin - P0;
            var ab = P1 - P0;
            t = Geometry.DotD(aq, ab) / Geometry.DotD(ab, ab);
            var eq = (t > 0.5 ? P1 : P0) - origin;
            var endpointDistance = Geometry.LengthD(eq);
            if (t > 0 && t < 1)
            {
                var orthoNormal = Geometry.NormalizeSafe(new Vector2(-ab.Y, ab.X));
                var orthoDist = Geometry.DotD(orthoNormal, aq);
                if (Math.Abs(orthoDist) < endpointDistance)
                {
                    return new SignedDistance(orthoDist, 0);
                }
            }
            var sign = Geometry.NonZeroSign(Geometry.Cross(aq, ab));
            var tangent = Geometry.NormalizeSafe(ab);
            var clampDir = Geometry.NormalizeSafe(eq);
            return new SignedDistance(sign * endpointDistance, Math.Abs(Geometry.DotD(tangent, clampDir)));
        }
    }
}
