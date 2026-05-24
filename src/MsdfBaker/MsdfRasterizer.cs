using System;
using System.Collections.Generic;
using System.Numerics;

namespace Radiant.MsdfBaker
{
    /// <summary>
    /// Generates a multi-channel SDF for a Shape into an RGB float buffer.
    /// Distances are stored in the buffer as raw signed distances; the caller
    /// is responsible for normalising them into [0,1] for an 8-bit texture by
    /// dividing by the chosen range and adding 0.5.
    /// </summary>
    public static class MsdfRasterizer
    {
        public static float[] Generate(Shape shape, int width, int height, double range, Vector2 scale, Vector2 translate)
        {
            var output = new float[width * height * 3];
            for (var y = 0; y < height; y++)
            {
                var py = (y + 0.5) / scale.Y - translate.Y;
                for (var x = 0; x < width; x++)
                {
                    var px = (x + 0.5) / scale.X - translate.X;
                    var origin = new Vector2((float)px, (float)py);
                    var r = SignedDistance.Infinity;
                    var g = SignedDistance.Infinity;
                    var b = SignedDistance.Infinity;
                    EdgeSegment? rEdge = null, gEdge = null, bEdge = null;
                    double rT = 0, gT = 0, bT = 0;

                    foreach (var contour in shape.Contours)
                    {
                        foreach (var edge in contour.Edges)
                        {
                            var sd = edge.SignedDistance(origin, out var t);
                            if ((edge.Color & EdgeColor.Red) != 0 && sd < r)
                            {
                                r = sd; rEdge = edge; rT = t;
                            }
                            if ((edge.Color & EdgeColor.Green) != 0 && sd < g)
                            {
                                g = sd; gEdge = edge; gT = t;
                            }
                            if ((edge.Color & EdgeColor.Blue) != 0 && sd < b)
                            {
                                b = sd; bEdge = edge; bT = t;
                            }
                        }
                    }

                    if (rEdge is not null) rEdge.DistanceToPseudoDistance(ref r, origin, rT);
                    if (gEdge is not null) gEdge.DistanceToPseudoDistance(ref g, origin, gT);
                    if (bEdge is not null) bEdge.DistanceToPseudoDistance(ref b, origin, bT);

                    var i = (y * width + x) * 3;
                    output[i + 0] = (float)(r.Distance / range + 0.5);
                    output[i + 1] = (float)(g.Distance / range + 0.5);
                    output[i + 2] = (float)(b.Distance / range + 0.5);
                }
            }

            CorrectSign(output, width, height, shape, range, scale, translate);
            return output;
        }

        /// <summary>
        /// Fix sign of MSDF channels using a horizontal-ray scanline winding
        /// test against the actual shape geometry. Each edge is approximated
        /// by 16 line segments — accurate enough for glyph outlines and
        /// avoids the per-channel-disagreement artefacts that pure median
        /// heuristics leave behind in glyph bounding boxes.
        /// </summary>
        private static void CorrectSign(float[] msdf, int width, int height, Shape shape, double range, Vector2 scale, Vector2 translate)
        {
            // Pre-flatten each edge to N line segments for fast scanline test.
            const int Subdiv = 16;
            var segments = new List<(Vector2 a, Vector2 b)>();
            foreach (var contour in shape.Contours)
            {
                foreach (var edge in contour.Edges)
                {
                    var prev = edge.Point(0);
                    for (var k = 1; k <= Subdiv; k++)
                    {
                        var t = (double)k / Subdiv;
                        var cur = edge.Point(t);
                        segments.Add((prev, cur));
                        prev = cur;
                    }
                }
            }

            for (var y = 0; y < height; y++)
            {
                var py = (y + 0.5) / scale.Y - translate.Y;
                for (var x = 0; x < width; x++)
                {
                    var px = (x + 0.5) / scale.X - translate.X;
                    var actuallyInside = IsInside((float)px, (float)py, segments);
                    var i = (y * width + x) * 3;
                    for (var c = 0; c < 3; c++)
                    {
                        var v = msdf[i + c];
                        if ((v > 0.5f) != actuallyInside)
                        {
                            msdf[i + c] = 1f - v;
                        }
                    }
                }
            }
        }

        private static bool IsInside(float x, float y, List<(Vector2 a, Vector2 b)> segments)
        {
            // Cast horizontal ray to +X, count edges crossing it.
            var crossings = 0;
            foreach (var (a, b) in segments)
            {
                // Edge straddles the horizontal line y?
                if ((a.Y <= y) == (b.Y <= y)) continue;
                // X coordinate of crossing.
                var t = (y - a.Y) / (b.Y - a.Y);
                var xCross = a.X + t * (b.X - a.X);
                if (xCross > x) crossings++;
            }
            return (crossings & 1) == 1;
        }

        private static float Median(float a, float b, float c)
            => Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
    }
}
