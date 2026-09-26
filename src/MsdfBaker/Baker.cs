using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using SixLabors.Fonts;
using SixLabors.Fonts.Unicode;

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

            // Fonts to take a glyph from when the primary has none, tried in order. Their glyphs are
            // baked at the same em size and made baseline-relative with their own ascender, so they
            // sit on the primary font's baseline at the primary font's scale.
            var fallbacks = request.FallbackFontPaths
                .Select(path => collection.Add(path, CultureInfo.InvariantCulture).CreateFont(request.GlyphPixelSize, FontStyle.Regular))
                .ToList();

            // emSize: pixel size of the EM square at this font size.
            var emSize = request.GlyphPixelSize;
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
            // Codepoints the primary font really has (including blank ones like space): the kerning
            // candidates. Kerning between a primary glyph and a fallback glyph is not defined.
            var present = new List<int>();

            foreach (var cp in request.Codepoints)
            {
                var (source, builder, advancePx) = Shape(font, cp);
                if (!builder.IsFallback)
                {
                    present.Add(cp);
                }
                else
                {
                    foreach (var fallback in fallbacks)
                    {
                        var candidate = Shape(fallback, cp);
                        if (!candidate.Builder.IsFallback && candidate.Builder.Result.Contours.Count > 0)
                        {
                            (source, builder, advancePx) = candidate;
                            break;
                        }
                    }
                }

                var shape = builder.Result;
                // Skip when no font has a glyph for this codepoint —
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
                    BearingY = (-TopAboveBaselinePx(source, cp, emSize) - padding) / emSize,
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
                Kerning = KerningExtractor.Extract(font, present, emSize),
            };

            Directory.CreateDirectory(request.OutputDirectory);
            var pngPath = Path.Combine(request.OutputDirectory, request.OutputName + ".png");
            var jsonPath = Path.Combine(request.OutputDirectory, request.OutputName + ".json");
            AtlasWriter.WritePng(pngPath, atlasPixels, request.AtlasSize, request.AtlasSize);
            AtlasWriter.WriteJson(jsonPath, manifest);
            return manifest;
        }

        // Lays out one codepoint in one font: its outline (IsFallback when the font has no glyph for
        // it) and its advance in pixels.
        private static (Font Source, GlyphShapeBuilder Builder, float AdvancePx) Shape(Font font, int codepoint)
        {
            var glyphString = char.ConvertFromUtf32(codepoint);
            var options = new TextOptions(font);
            var advancePx = TextMeasurer.MeasureAdvance(glyphString, options).Width;

            var builder = new GlyphShapeBuilder();
            new TextRenderer(builder).RenderText(glyphString, options);
            return (font, builder, advancePx);
        }

        // How far the glyph's outline rises above the baseline, in pixels at this em size.
        //
        // The stored BearingY has to be baseline-relative (the TTF / FreeType convention the runtime
        // DrawText path expects), but the layout bounds SixLabors renders with are measured from the
        // top of the line, which each font places differently. For Inter that happens to be its
        // ascender. Noto Sans Math and Noto Sans Symbols use other vertical metrics, so a fallback
        // glyph placed that way floats above the baseline. The outline's own yMax is measured from the
        // baseline in every font.
        private static float TopAboveBaselinePx(Font font, int codepoint, int emSize)
        {
            if (!font.TryGetGlyphs(new CodePoint(codepoint), out var glyphs) || glyphs.Count == 0)
            {
                throw new InvalidOperationException($"{font.Name} has an outline for U+{codepoint:X4} but no glyph metrics.");
            }
            // At the origin and 72 dpi the box is in pixels, y-down from the baseline: its top is -yMax.
            return -glyphs[0].BoundingBox(GlyphLayoutMode.Horizontal, Vector2.Zero, 72f).Y;
        }
    }
}
