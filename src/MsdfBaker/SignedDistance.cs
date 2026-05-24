using System;

namespace Radiant.MsdfBaker
{
    /// <summary>
    /// Signed distance from a point to an edge plus the cosine of the angle
    /// between the edge tangent and the perpendicular to the point — used to
    /// break ties consistently between adjacent edges that share an endpoint.
    /// </summary>
    public readonly record struct SignedDistance(double Distance, double Dot) : IComparable<SignedDistance>
    {
        public static readonly SignedDistance Infinity = new(double.NegativeInfinity, 1.0);

        public int CompareTo(SignedDistance other)
        {
            var a = Math.Abs(Distance);
            var b = Math.Abs(other.Distance);
            if (a != b) return a < b ? -1 : 1;
            return Dot.CompareTo(other.Dot);
        }

        public static bool operator <(SignedDistance a, SignedDistance b) => a.CompareTo(b) < 0;
        public static bool operator >(SignedDistance a, SignedDistance b) => a.CompareTo(b) > 0;
        public static bool operator <=(SignedDistance a, SignedDistance b) => a.CompareTo(b) <= 0;
        public static bool operator >=(SignedDistance a, SignedDistance b) => a.CompareTo(b) >= 0;
    }
}
