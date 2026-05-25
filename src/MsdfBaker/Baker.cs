using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using SixLabors.Fonts;

namespace Radiant.MsdfBaker
{
    public static class Baker
    {
        public static AtlasManifest Bake(BakeRequest request)
        {
            var collection = new FontCollection();
            var family = collection.Add(request.FontPath, CultureInfo.InvariantCulture);
            var font = family.CreateFont(request.GlyphPixelSize, FontStyle.Regular);
            var metrics = font.FontMetrics;

            // emSize: pixel size of the EM square at this font size.
            var emSize = request.GlyphPixelSize;
            // SixLabors.Fonts renders with the pen origin at the TOP of the EM
            // box, Y-down. Bake-time bounds.Y is therefore offset from EM-top
            // rather than from baseline. Subtract the ascender so the stored
            // BearingY is baseline-relative (matches TTF / FreeType convention
            // expected by the runtime DrawText path).
            var ascenderPx = metrics.HorizontalMetrics.Ascender * (float)emSize / metrics.UnitsPerEm;
            var packer = new ShelfPacker(request.AtlasSize, request.AtlasSize, padding: 2);
            var atlasPixels = new float[request.AtlasSize * request.AtlasSize * 3];
            for (var i = 0; i < atlasPixels.Length; i += 3)
            {
                // Sentinel for unused atlas regions: median == 0.5 → outside.
                atlasPixels[i + 0] = 0f;
                atlasPixels[i + 1] = 0f;
                atlasPixels[i + 2] = 0f;
            }

            var glyphs = new List<AtlasGlyph>();

            foreach (var cp in request.Codepoints)
            {
                var glyphString = char.ConvertFromUtf32(cp);
                var options = new TextOptions(font);
                var advanceRect = TextMeasurer.MeasureAdvance(glyphString, options);
                var advancePx = advanceRect.Width;

                var builder = new GlyphShapeBuilder();
                var renderer = new TextRenderer(builder);
                renderer.RenderText(glyphString, options);

                var shape = builder.Result;
                // Skip when the source font has no glyph for this codepoint —
                // SixLabors fires the .notdef placeholder (IsFallback=true) which
                // post-bake is indistinguishable from a real glyph. Leaving an
                // empty manifest entry lets a runtime font-fallback chain try
                // the next font.
                if (builder.IsFallback || shape.Contours.Count == 0)
                {
                    glyphs.Add(new AtlasGlyph
                    {
                        Codepoint = cp,
                        Advance = advancePx / emSize,
                    });
                    continue;
                }

                EdgeColoring.Apply(shape);

                var bounds = builder.Bounds;
                var padding = (int)Math.Ceiling(request.DistanceRangePx);
                var w = (int)Math.Ceiling(bounds.Width) + padding * 2;
                var h = (int)Math.Ceiling(bounds.Height) + padding * 2;

                if (!packer.TryPack(w, h, out var px, out var py))
                {
                    throw new InvalidOperationException(
                        $"Atlas overflow at codepoint U+{cp:X4}. Increase AtlasSize beyond {request.AtlasSize}.");
                }

                var scale = new Vector2(1f, 1f);
                var translate = new Vector2(
                    (float)(-bounds.X + padding),
                    (float)(-bounds.Y + padding));

                var msdf = MsdfRasterizer.Generate(shape, w, h, request.DistanceRangePx, scale, translate);

                // Blit into atlas.
                for (var yy = 0; yy < h; yy++)
                {
                    for (var xx = 0; xx < w; xx++)
                    {
                        var src = (yy * w + xx) * 3;
                        var dst = ((py + yy) * request.AtlasSize + (px + xx)) * 3;
                        atlasPixels[dst + 0] = msdf[src + 0];
                        atlasPixels[dst + 1] = msdf[src + 1];
                        atlasPixels[dst + 2] = msdf[src + 2];
                    }
                }

                glyphs.Add(new AtlasGlyph
                {
                    Codepoint = cp,
                    U0 = (float)px / request.AtlasSize,
                    V0 = (float)py / request.AtlasSize,
                    U1 = (float)(px + w) / request.AtlasSize,
                    V1 = (float)(py + h) / request.AtlasSize,
                    Width = w / (float)emSize,
                    Height = h / (float)emSize,
                    BearingX = (float)(bounds.X - padding) / emSize,
                    BearingY = (float)(bounds.Y - padding - ascenderPx) / emSize,
                    Advance = advancePx / emSize,
                });
            }

            var manifest = new AtlasManifest
            {
                FontFamily = family.Name,
                AtlasWidth = request.AtlasSize,
                AtlasHeight = request.AtlasSize,
                GlyphPixelSize = request.GlyphPixelSize,
                DistanceRange = request.DistanceRangePx,
                LineHeight = metrics.HorizontalMetrics.AdvanceHeightMax * (float)emSize / metrics.UnitsPerEm / emSize,
                Ascender = metrics.HorizontalMetrics.Ascender * (float)emSize / metrics.UnitsPerEm / emSize,
                Descender = metrics.HorizontalMetrics.Descender * (float)emSize / metrics.UnitsPerEm / emSize,
                Glyphs = glyphs,
                Kerning = [],
            };

            Directory.CreateDirectory(request.OutputDirectory);
            var pngPath = Path.Combine(request.OutputDirectory, request.OutputName + ".png");
            var jsonPath = Path.Combine(request.OutputDirectory, request.OutputName + ".json");
            AtlasWriter.WritePng(pngPath, atlasPixels, request.AtlasSize, request.AtlasSize);
            AtlasWriter.WriteJson(jsonPath, manifest);
            return manifest;
        }
    }
}
