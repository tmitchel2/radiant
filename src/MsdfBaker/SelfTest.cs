using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Radiant.MsdfBaker
{
    /// <summary>
    /// Self-test: load a baked atlas and render a string at a given pixel
    /// height into a PNG. Uses the same MSDF resolve logic the runtime shader
    /// will use (median(R,G,B), screen-space derivative for AA width).
    /// </summary>
    public static class SelfTest
    {
        private static readonly JsonSerializerOptions JsonOptions = new();

        public static void RenderString(string atlasDir, string atlasName, string text, float pixelHeight, string outputPath)
        {
            var jsonPath = Path.Combine(atlasDir, atlasName + ".json");
            var pngPath = Path.Combine(atlasDir, atlasName + ".png");
            var manifest = JsonSerializer.Deserialize<AtlasManifest>(File.ReadAllText(jsonPath), JsonOptions)
                ?? throw new InvalidDataException("Failed to deserialize atlas manifest.");
            using var atlas = Image.Load<Rgba32>(pngPath);
            var atlasPixels = new float[atlas.Width * atlas.Height * 3];
            atlas.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < accessor.Width; x++)
                    {
                        var i = (y * accessor.Width + x) * 3;
                        atlasPixels[i + 0] = row[x].R / 255f;
                        atlasPixels[i + 1] = row[x].G / 255f;
                        atlasPixels[i + 2] = row[x].B / 255f;
                    }
                }
            });

            // Pixel-space distance range at the rendered size: scales with text height.
            var renderScale = pixelHeight; // 1 em = pixelHeight at render time.
            var pxRange = manifest.DistanceRange * (pixelHeight / manifest.GlyphPixelSize);

            // Layout: compute the bounding box of the entire string in pixels.
            var glyphTable = new Dictionary<int, AtlasGlyph>(manifest.Glyphs.Count);
            foreach (var g in manifest.Glyphs) glyphTable[g.Codepoint] = g;

            var ascentPx = manifest.Ascender * renderScale;
            var descentPx = -manifest.Descender * renderScale;
            var lineH = (int)Math.Ceiling(ascentPx + descentPx) + 2;

            const float PenStart = 4f;
            float penX = PenStart;
            float totalWidth = PenStart;
            foreach (var rune in text.EnumerateRunes())
            {
                if (!glyphTable.TryGetValue(rune.Value, out var g)) continue;
                totalWidth += g.Advance * renderScale;
            }
            var width = (int)Math.Ceiling(totalWidth) + 4;
            var height = lineH;

            using var output = new Image<Rgba32>(width, height, new Rgba32(255, 255, 255, 255));

            foreach (var rune in text.EnumerateRunes())
            {
                if (!glyphTable.TryGetValue(rune.Value, out var g))
                {
                    penX += 0.25f * renderScale;
                    continue;
                }
                if (g.Width > 0 && g.Height > 0)
                {
                    DrawGlyph(output, atlasPixels, g, manifest, penX, ascentPx, renderScale, pxRange);
                }
                penX += g.Advance * renderScale;
            }

            output.SaveAsPng(outputPath);
        }

        private static void DrawGlyph(
            Image<Rgba32> output,
            float[] atlasPixels,
            AtlasGlyph glyph,
            AtlasManifest manifest,
            float penX,
            float baselineY,
            float renderScale,
            float pxRange)
        {
            var atlasW = manifest.AtlasWidth;
            var atlasH = manifest.AtlasHeight;

            var glyphPixelW = glyph.Width * renderScale;
            var glyphPixelH = glyph.Height * renderScale;

            var x0 = penX + glyph.BearingX * renderScale;
            var y0 = baselineY + glyph.BearingY * renderScale;

            var px0 = Math.Max((int)Math.Floor(x0), 0);
            var py0 = Math.Max((int)Math.Floor(y0), 0);
            var px1 = Math.Min((int)Math.Ceiling(x0 + glyphPixelW), output.Width);
            var py1 = Math.Min((int)Math.Ceiling(y0 + glyphPixelH), output.Height);

            output.ProcessPixelRows(outAccess =>
            {
                for (var py = py0; py < py1; py++)
                {
                    var outRow = outAccess.GetRowSpan(py);
                    for (var px = px0; px < px1; px++)
                    {
                        var lx = (px + 0.5f - x0) / glyphPixelW;
                        var ly = (py + 0.5f - y0) / glyphPixelH;
                        var u = glyph.U0 + lx * (glyph.U1 - glyph.U0);
                        var v = glyph.V0 + ly * (glyph.V1 - glyph.V0);

                        var ax = u * atlasW - 0.5f;
                        var ay = v * atlasH - 0.5f;
                        var sample = SampleBilinear(atlasPixels, atlasW, atlasH, ax, ay);
                        var sd = Median(sample.X, sample.Y, sample.Z);
                        var screenPxDist = pxRange * (sd - 0.5f);
                        var alpha = Math.Clamp(screenPxDist + 0.5f, 0f, 1f);
                        if (alpha <= 0f) continue;

                        var src = outRow[px];
                        var bg = new System.Numerics.Vector3(src.R, src.G, src.B) / 255f;
                        var fg = new System.Numerics.Vector3(0f, 0f, 0f);
                        var blended = (1f - alpha) * bg + alpha * fg;
                        outRow[px] = new Rgba32(
                            (byte)Math.Round(blended.X * 255f),
                            (byte)Math.Round(blended.Y * 255f),
                            (byte)Math.Round(blended.Z * 255f),
                            255);
                    }
                }
            });
        }

        private static System.Numerics.Vector3 SampleBilinear(float[] pixels, int w, int h, float x, float y)
        {
            var x0 = (int)Math.Floor(x);
            var y0 = (int)Math.Floor(y);
            var x1 = x0 + 1;
            var y1 = y0 + 1;
            var fx = x - x0;
            var fy = y - y0;
            x0 = Math.Clamp(x0, 0, w - 1);
            y0 = Math.Clamp(y0, 0, h - 1);
            x1 = Math.Clamp(x1, 0, w - 1);
            y1 = Math.Clamp(y1, 0, h - 1);
            var p00 = Read(pixels, w, x0, y0);
            var p10 = Read(pixels, w, x1, y0);
            var p01 = Read(pixels, w, x0, y1);
            var p11 = Read(pixels, w, x1, y1);
            var a = System.Numerics.Vector3.Lerp(p00, p10, fx);
            var b = System.Numerics.Vector3.Lerp(p01, p11, fx);
            return System.Numerics.Vector3.Lerp(a, b, fy);
        }

        private static System.Numerics.Vector3 Read(float[] pixels, int w, int x, int y)
        {
            var i = (y * w + x) * 3;
            return new System.Numerics.Vector3(pixels[i + 0], pixels[i + 1], pixels[i + 2]);
        }

        private static float Median(float a, float b, float c)
            => Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
    }
}
