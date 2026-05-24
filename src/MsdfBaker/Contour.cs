using System.Collections.Generic;
using System.Numerics;

namespace Radiant.MsdfBaker
{
    public sealed class Contour
    {
        public List<EdgeSegment> Edges { get; } = [];

        public int Winding()
        {
            if (Edges.Count == 0) return 0;
            double total = 0;
            if (Edges.Count == 1)
            {
                var a = Edges[0].Point(0);
                var b = Edges[0].Point(1.0 / 3.0);
                var c = Edges[0].Point(2.0 / 3.0);
                total += Shoelace(a, b);
                total += Shoelace(b, c);
                total += Shoelace(c, a);
            }
            else
            {
                var prev = Edges[^1].Point(0);
                foreach (var edge in Edges)
                {
                    var cur = edge.Point(0);
                    total += Shoelace(prev, cur);
                    prev = cur;
                }
            }
            return total > 0 ? 1 : total < 0 ? -1 : 0;
        }

        private static double Shoelace(Vector2 a, Vector2 b)
            => ((double)b.X - a.X) * ((double)a.Y + b.Y);
    }
}
