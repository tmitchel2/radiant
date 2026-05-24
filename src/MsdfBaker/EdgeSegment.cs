using System;
using System.Numerics;

namespace Radiant.MsdfBaker
{
    public abstract class EdgeSegment
    {
        public EdgeColor Color { get; set; } = EdgeColor.White;

        public abstract Vector2 Point(double t);

        public abstract Vector2 Direction(double t);

        public abstract SignedDistance SignedDistance(Vector2 origin, out double t);

        /// <summary>
        /// Convert the perpendicular-distance result returned by SignedDistance
        /// into a "pseudo-distance" suitable for MSDF edges that meet at sharp
        /// corners. Off the end of the segment, we use the perpendicular to the
        /// extended tangent instead of the clamped endpoint distance.
        /// </summary>
        public void DistanceToPseudoDistance(ref SignedDistance distance, Vector2 origin, double t)
        {
            if (t < 0)
            {
                var dir = Geometry.NormalizeSafe(Direction(0));
                var aq = origin - Point(0);
                var ts = Geometry.DotD(aq, dir);
                if (ts < 0)
                {
                    var pseudo = Geometry.Cross(aq, dir);
                    if (Math.Abs(pseudo) <= Math.Abs(distance.Distance))
                    {
                        distance = new SignedDistance(pseudo, 0);
                    }
                }
            }
            else if (t > 1)
            {
                var dir = Geometry.NormalizeSafe(Direction(1));
                var bq = origin - Point(1);
                var ts = Geometry.DotD(bq, dir);
                if (ts > 0)
                {
                    var pseudo = Geometry.Cross(bq, dir);
                    if (Math.Abs(pseudo) <= Math.Abs(distance.Distance))
                    {
                        distance = new SignedDistance(pseudo, 0);
                    }
                }
            }
        }
    }
}
