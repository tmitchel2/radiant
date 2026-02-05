using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Graphics2D;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Radiant.Tests.Graphics2D.Visual
{
    /// <summary>
    /// CPU-based software rasterizer that mimics the GPU pipeline for visual regression testing.
    /// Supports filled triangles (barycentric) and lines (Bresenham) with alpha blending.
    /// </summary>
    internal static class SoftwareRasterizer
    {
        public static Image<Rgba32> Render(
            IReadOnlyList<Vertex2D> filledVertices,
            IReadOnlyList<Vertex2D> lineVertices,
            Matrix4x4 projectionMatrix,
            int width,
            int height,
            Rgba32 clearColor)
        {
            var image = new Image<Rgba32>(width, height);

            // Clear
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    image[x, y] = clearColor;
                }
            }

            // Rasterize filled triangles (groups of 3 vertices)
            for (var i = 0; i + 2 < filledVertices.Count; i += 3)
            {
                RasterizeTriangle(image, filledVertices[i], filledVertices[i + 1], filledVertices[i + 2],
                    projectionMatrix, width, height);
            }

            // Rasterize lines (groups of 2 vertices)
            for (var i = 0; i + 1 < lineVertices.Count; i += 2)
            {
                RasterizeLine(image, lineVertices[i], lineVertices[i + 1],
                    projectionMatrix, width, height);
            }

            return image;
        }

        private static Vector2 ProjectToPixel(Vector2 position, Matrix4x4 matrix, int width, int height)
        {
            var clip = Vector4.Transform(new Vector4(position, 0f, 1f), matrix);
            var ndcX = clip.X / clip.W;
            var ndcY = clip.Y / clip.W;

            var px = (ndcX + 1f) / 2f * width;
            var py = (1f - ndcY) / 2f * height;

            return new Vector2(px, py);
        }

        private static void RasterizeTriangle(
            Image<Rgba32> image,
            Vertex2D v0, Vertex2D v1, Vertex2D v2,
            Matrix4x4 matrix, int width, int height)
        {
            var p0 = ProjectToPixel(v0.Position, matrix, width, height);
            var p1 = ProjectToPixel(v1.Position, matrix, width, height);
            var p2 = ProjectToPixel(v2.Position, matrix, width, height);

            // Bounding box
            var minX = (int)MathF.Floor(Math.Min(p0.X, Math.Min(p1.X, p2.X)));
            var maxX = (int)MathF.Ceiling(Math.Max(p0.X, Math.Max(p1.X, p2.X)));
            var minY = (int)MathF.Floor(Math.Min(p0.Y, Math.Min(p1.Y, p2.Y)));
            var maxY = (int)MathF.Ceiling(Math.Max(p0.Y, Math.Max(p1.Y, p2.Y)));

            minX = Math.Max(0, minX);
            maxX = Math.Min(width - 1, maxX);
            minY = Math.Max(0, minY);
            maxY = Math.Min(height - 1, maxY);

            for (var py = minY; py <= maxY; py++)
            {
                for (var px = minX; px <= maxX; px++)
                {
                    var point = new Vector2(px + 0.5f, py + 0.5f);

                    var (w0, w1, w2) = BarycentricCoords(point, p0, p1, p2);

                    // Accept both winding orders (CullMode.None)
                    if (w0 < 0 || w1 < 0 || w2 < 0)
                    {
                        if (w0 > 0 || w1 > 0 || w2 > 0)
                            continue;
                    }

                    // Interpolate color
                    var color = new Vector4(
                        w0 * v0.Color.X + w1 * v1.Color.X + w2 * v2.Color.X,
                        w0 * v0.Color.Y + w1 * v1.Color.Y + w2 * v2.Color.Y,
                        w0 * v0.Color.Z + w1 * v1.Color.Z + w2 * v2.Color.Z,
                        w0 * v0.Color.W + w1 * v1.Color.W + w2 * v2.Color.W);

                    BlendPixel(image, px, py, color);
                }
            }
        }

        private static (float w0, float w1, float w2) BarycentricCoords(
            Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var v0 = c - a;
            var v1 = b - a;
            var v2 = p - a;

            var dot00 = Vector2.Dot(v0, v0);
            var dot01 = Vector2.Dot(v0, v1);
            var dot02 = Vector2.Dot(v0, v2);
            var dot11 = Vector2.Dot(v1, v1);
            var dot12 = Vector2.Dot(v1, v2);

            var invDenom = 1f / (dot00 * dot11 - dot01 * dot01);
            var u = (dot00 * dot12 - dot01 * dot02) * invDenom;
            var v = (dot11 * dot02 - dot01 * dot12) * invDenom;

            return (1f - u - v, u, v);
        }

        private static void RasterizeLine(
            Image<Rgba32> image,
            Vertex2D v0, Vertex2D v1,
            Matrix4x4 matrix, int width, int height)
        {
            var p0 = ProjectToPixel(v0.Position, matrix, width, height);
            var p1 = ProjectToPixel(v1.Position, matrix, width, height);

            // Bresenham's line algorithm
            var x0 = (int)MathF.Round(p0.X);
            var y0 = (int)MathF.Round(p0.Y);
            var x1 = (int)MathF.Round(p1.X);
            var y1 = (int)MathF.Round(p1.Y);

            var dx = Math.Abs(x1 - x0);
            var dy = Math.Abs(y1 - y0);
            var sx = x0 < x1 ? 1 : -1;
            var sy = y0 < y1 ? 1 : -1;
            var err = dx - dy;

            // Interpolate color along line
            var totalDist = MathF.Sqrt(dx * dx + dy * dy);

            while (true)
            {
                if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
                {
                    // Linearly interpolate color based on distance
                    float t;
                    if (totalDist < 0.001f)
                    {
                        t = 0f;
                    }
                    else
                    {
                        var currentDx = x0 - (int)MathF.Round(p0.X);
                        var currentDy = y0 - (int)MathF.Round(p0.Y);
                        t = MathF.Sqrt(currentDx * currentDx + currentDy * currentDy) / totalDist;
                    }

                    var color = Vector4.Lerp(v0.Color, v1.Color, t);
                    BlendPixel(image, x0, y0, color);
                }

                if (x0 == x1 && y0 == y1)
                    break;

                var e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private static void BlendPixel(Image<Rgba32> image, int x, int y, Vector4 srcColor)
        {
            // SrcAlpha / OneMinusSrcAlpha blending (matching GPU pipeline)
            var srcA = srcColor.W;
            var dstPixel = image[x, y];
            var dstColor = new Vector4(dstPixel.R / 255f, dstPixel.G / 255f, dstPixel.B / 255f, dstPixel.A / 255f);

            var outR = srcColor.X * srcA + dstColor.X * (1f - srcA);
            var outG = srcColor.Y * srcA + dstColor.Y * (1f - srcA);
            var outB = srcColor.Z * srcA + dstColor.Z * (1f - srcA);
            var outA = srcA + dstColor.W * (1f - srcA);

            image[x, y] = new Rgba32(
                (byte)Math.Clamp(outR * 255f + 0.5f, 0, 255),
                (byte)Math.Clamp(outG * 255f + 0.5f, 0, 255),
                (byte)Math.Clamp(outB * 255f + 0.5f, 0, 255),
                (byte)Math.Clamp(outA * 255f + 0.5f, 0, 255));
        }
    }
}
