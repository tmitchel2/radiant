using System;
using System.Collections.Generic;
using System.Numerics;

namespace Radiant.MsdfBaker
{
    /// <summary>
    /// Chlumsky's "edgeColoringSimple": walk each contour, splitting it at
    /// corners (cos(angle) &lt; threshold) and alternating R/G/B colour
    /// channels so adjacent edges share at most one channel. This is what
    /// makes the median(R,G,B) recovery at sample time produce a sharp
    /// distance field across corners.
    /// </summary>
    public static class EdgeColoring
    {
        public static void Apply(Shape shape, double angleThreshold = 3.0)
        {
            var crossThreshold = Math.Sin(angleThreshold);
            foreach (var contour in shape.Contours)
            {
                ColorContour(contour, crossThreshold);
            }
        }

        private static void ColorContour(Contour contour, double crossThreshold)
        {
            if (contour.Edges.Count == 0) return;

            var corners = new List<int>();
            var prevDir = Geometry.NormalizeSafe(contour.Edges[^1].Direction(1));
            for (var i = 0; i < contour.Edges.Count; i++)
            {
                var edge = contour.Edges[i];
                if (IsCorner(prevDir, Geometry.NormalizeSafe(edge.Direction(0)), crossThreshold))
                {
                    corners.Add(i);
                }
                prevDir = Geometry.NormalizeSafe(edge.Direction(1));
            }

            if (corners.Count == 0)
            {
                foreach (var e in contour.Edges) e.Color = EdgeColor.White;
                return;
            }

            if (corners.Count == 1)
            {
                Span<EdgeColor> colors = stackalloc EdgeColor[3] { EdgeColor.White, EdgeColor.White, EdgeColor.White };
                SwitchColor(ref colors[0], EdgeColor.Black);
                SwitchColor(ref colors[2], EdgeColor.Black);
                colors[1] = (EdgeColor)((int)colors[0] & (int)colors[2]);
                if (colors[1] == EdgeColor.Black)
                {
                    colors[1] = EdgeColor.White;
                }
                var corner = corners[0];
                if (contour.Edges.Count >= 3)
                {
                    var m = contour.Edges.Count;
                    for (var i = 0; i < m; i++)
                    {
                        var ci = (i + corner) % m;
                        var idx = (3 + 3 * (m - 1) / 2 - i) / m;
                        contour.Edges[ci].Color = colors[Math.Clamp(idx, 0, 2)];
                    }
                }
                else
                {
                    // Few-edge path: split into thirds via t-subdivision is
                    // out of scope here. Just stamp consistent colours.
                    contour.Edges[corner].Color = colors[1];
                    for (var i = 0; i < contour.Edges.Count; i++)
                    {
                        if (i != corner) contour.Edges[i].Color = colors[0];
                    }
                }
                return;
            }

            var cornerCount = corners.Count;
            var spline = 0;
            var start = corners[0];
            var m2 = contour.Edges.Count;
            var color = EdgeColor.White;
            SwitchColor(ref color, EdgeColor.Black);
            var initial = color;
            for (var i = 0; i < m2; i++)
            {
                var idx = (start + i) % m2;
                if (spline + 1 < cornerCount && corners[spline + 1] == idx)
                {
                    spline++;
                    SwitchColor(ref color, (spline == cornerCount - 1) ? initial : EdgeColor.Black);
                }
                contour.Edges[idx].Color = color;
            }
        }

        private static bool IsCorner(Vector2 a, Vector2 b, double crossThreshold)
        {
            if (Geometry.DotD(a, b) <= 0) return true;
            return Math.Abs(Geometry.Cross(a, b)) > crossThreshold;
        }

        private static void SwitchColor(ref EdgeColor color, EdgeColor banned)
        {
            EdgeColor combined = color & banned;
            if (combined == EdgeColor.Red || combined == EdgeColor.Green || combined == EdgeColor.Blue)
            {
                color = combined ^ EdgeColor.White;
                return;
            }
            if (color == EdgeColor.Black || color == EdgeColor.White)
            {
                color = (banned != EdgeColor.Red) ? EdgeColor.Cyan : EdgeColor.Yellow;
                return;
            }
            var shifted = ((int)color << 1) | ((int)color >> 2);
            color = (EdgeColor)(shifted & (int)EdgeColor.White);
            if ((color & banned) != EdgeColor.Black)
            {
                shifted = ((int)color << 1) | ((int)color >> 2);
                color = (EdgeColor)(shifted & (int)EdgeColor.White);
            }
        }
    }
}
