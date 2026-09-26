// Canvas.Line is ported from font-rs (https://github.com/raphlinus/font-rs), Copyright 2015 Google
// Inc., licensed under the Apache License, Version 2.0 (see THIRD-PARTY-NOTICES.md).

using System;
using System.Buffers;
using System.Numerics;

namespace Radiant.Text;

/// <summary>
/// Rasterizes glyph outlines to coverage bitmaps with exact area coverage: each pixel gets the
/// fraction of its area inside the outline, which is the anti-aliasing text needs at small sizes.
/// <para>
/// The method is the signed-area accumulation of font-rs (Raph Levien): each edge adds, to the
/// pixels it crosses, how much area it bounds to their right, and a running sum along each row
/// turns those into coverage. Curves are flattened to lines first. Overlapping contours, which
/// variable fonts have, are handled by clamping the sum's magnitude to one, which gives the
/// non-zero fill rule for contours wound the same way.
/// </para>
/// </summary>
public static class GlyphRasterizer
{
    // How far a flattened curve may stray from the true curve, in pixels.
    private const float Tolerance = 0.05f;

    /// <summary>Rasterizes an outline.</summary>
    /// <param name="outline">The outline, in font units with y up.</param>
    /// <param name="scale">Pixels per font unit: the size in pixels over the font's units per em.</param>
    /// <param name="offset">
    /// Where the pen sits within its pixel, from 0 to 1 on each axis: a glyph drawn a quarter of a
    /// pixel right of a pixel boundary is rasterized with an x offset of 0.25.
    /// </param>
    public static GlyphBitmap Rasterize(GlyphOutline outline, float scale, Vector2 offset = default)
    {
        ArgumentNullException.ThrowIfNull(outline);
        if (outline.IsEmpty)
        {
            return GlyphBitmap.Empty;
        }

        var (min, max) = outline.Bounds;
        var left = (int)MathF.Floor(min.X * scale + offset.X);
        var right = (int)MathF.Ceiling(max.X * scale + offset.X);
        var top = (int)MathF.Floor(-max.Y * scale + offset.Y);
        var bottom = (int)MathF.Ceiling(-min.Y * scale + offset.Y);
        var width = right - left;
        var height = bottom - top;
        if (width <= 0 || height <= 0)
        {
            return GlyphBitmap.Empty;
        }

        // Two spare cells a row: an edge on the right boundary writes one or two cells past it.
        var stride = width + 2;
        var cells = ArrayPool<float>.Shared.Rent(stride * height);
        try
        {
            Array.Clear(cells, 0, stride * height);
            var canvas = new Canvas(cells, stride, width, height, scale, offset, left, top);
            Trace(outline, ref canvas);
            return new GlyphBitmap(left, top, width, height, Accumulate(cells, stride, width, height));
        }
        finally
        {
            ArrayPool<float>.Shared.Return(cells);
        }
    }

    private static void Trace(GlyphOutline outline, ref Canvas canvas)
    {
        var points = outline.Points;
        var p = 0;
        Vector2 start = default, current = default;
        var open = false;
        foreach (var verb in outline.Verbs)
        {
            switch (verb)
            {
                case PathVerb.MoveTo:
                    if (open)
                    {
                        canvas.Line(current, start);
                    }
                    start = current = canvas.Map(points[p++]);
                    open = true;
                    break;
                case PathVerb.LineTo:
                {
                    var end = canvas.Map(points[p++]);
                    canvas.Line(current, end);
                    current = end;
                    break;
                }
                case PathVerb.QuadTo:
                {
                    var control = canvas.Map(points[p++]);
                    var end = canvas.Map(points[p++]);
                    canvas.Quad(current, control, end);
                    current = end;
                    break;
                }
                case PathVerb.CubicTo:
                {
                    var c1 = canvas.Map(points[p++]);
                    var c2 = canvas.Map(points[p++]);
                    var end = canvas.Map(points[p++]);
                    canvas.Cubic(current, c1, c2, end);
                    current = end;
                    break;
                }
                case PathVerb.Close:
                    if (open)
                    {
                        canvas.Line(current, start);
                        current = start;
                        open = false;
                    }
                    break;
            }
        }
        if (open)
        {
            canvas.Line(current, start);
        }
    }

    private static byte[] Accumulate(float[] cells, int stride, int width, int height)
    {
        var coverage = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            var sum = 0f;
            var row = y * stride;
            for (var x = 0; x < width; x++)
            {
                sum += cells[row + x];
                coverage[y * width + x] = (byte)(MathF.Min(MathF.Abs(sum), 1f) * 255f + 0.5f);
            }
        }
        return coverage;
    }

    private readonly ref struct Canvas(
        float[] cells, int stride, int width, int height, float scale, Vector2 offset, int left, int top)
    {
        /// <summary>From font units (y up) to bitmap pixels (y down), kept inside the bitmap.</summary>
        public Vector2 Map(Vector2 point) => new(
            Math.Clamp(point.X * scale + offset.X - left, 0f, width),
            Math.Clamp(-point.Y * scale + offset.Y - top, 0f, height));

        public void Quad(Vector2 p0, Vector2 p1, Vector2 p2)
        {
            // A quadratic's distance from its chord is |p0 - 2 p1 + p2| / 4 at most, and n lines
            // cut that by n², so n = sqrt(dd / (8 tolerance)) segments stay within the tolerance.
            var dd = (p0 - 2f * p1 + p2).Length();
            var n = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(dd / (8f * Tolerance))));
            var previous = p0;
            for (var i = 1; i <= n; i++)
            {
                var t = i / (float)n;
                var u = 1f - t;
                var next = u * u * p0 + 2f * u * t * p1 + t * t * p2;
                Line(previous, next);
                previous = next;
            }
        }

        public void Cubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            // The same bound, with the cubic's second derivative (at most 6 max|p_i - 2p_{i+1} + p_{i+2}|).
            var dd = MathF.Max((p0 - 2f * p1 + p2).Length(), (p1 - 2f * p2 + p3).Length());
            var n = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(0.75f * dd / Tolerance)));
            var previous = p0;
            for (var i = 1; i <= n; i++)
            {
                var t = i / (float)n;
                var u = 1f - t;
                var next = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
                Line(previous, next);
                previous = next;
            }
        }

        /// <summary>
        /// Adds a line's signed area to the cells of each row it crosses: the area between the
        /// line and the row's right end, split between the cells the line passes through (as a
        /// trapezoid) and the cell after them (the rest, which the row's running sum carries on).
        /// </summary>
        public void Line(Vector2 p0, Vector2 p1)
        {
            if (MathF.Abs(p0.Y - p1.Y) <= float.Epsilon)
            {
                return;
            }
            var direction = 1f;
            if (p0.Y > p1.Y)
            {
                direction = -1f;
                (p0, p1) = (p1, p0);
            }
            var dxdy = (p1.X - p0.X) / (p1.Y - p0.Y);
            var x = p0.X;
            var yEnd = Math.Min(height, (int)MathF.Ceiling(p1.Y));
            for (var y = (int)p0.Y; y < yEnd; y++)
            {
                var row = y * stride;
                var dy = MathF.Min(y + 1, p1.Y) - MathF.Max(y, p0.Y);
                var xNext = x + dxdy * dy;
                var d = dy * direction;
                var (x0, x1) = x < xNext ? (x, xNext) : (xNext, x);
                var x0Floor = MathF.Floor(x0);
                var x0i = (int)x0Floor;
                var x1Ceil = MathF.Ceiling(x1);
                var x1i = (int)x1Ceil;
                if (x1i <= x0i + 1)
                {
                    // Within one cell: its share is the part of the cell to the line's right.
                    var xm = 0.5f * (x + xNext) - x0Floor;
                    cells[row + x0i] += d - d * xm;
                    cells[row + x0i + 1] += d * xm;
                }
                else
                {
                    var s = 1f / (x1 - x0);
                    var x0f = x0 - x0Floor;
                    var a0 = 0.5f * s * (1f - x0f) * (1f - x0f);
                    var x1f = x1 - x1Ceil + 1f;
                    var am = 0.5f * s * x1f * x1f;
                    cells[row + x0i] += d * a0;
                    if (x1i == x0i + 2)
                    {
                        cells[row + x0i + 1] += d * (1f - a0 - am);
                    }
                    else
                    {
                        var a1 = s * (1.5f - x0f);
                        cells[row + x0i + 1] += d * (a1 - a0);
                        for (var xi = x0i + 2; xi < x1i - 1; xi++)
                        {
                            cells[row + xi] += d * s;
                        }
                        var a2 = a1 + (x1i - x0i - 3) * s;
                        cells[row + x1i - 1] += d * (1f - a2 - am);
                    }
                    cells[row + x1i] += d * am;
                }
                x = xNext;
            }
        }
    }
}
