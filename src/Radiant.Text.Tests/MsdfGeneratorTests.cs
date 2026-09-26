using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Text.Msdf;

namespace Radiant.Text.Tests;

/// <summary>What <see cref="MsdfGenerator"/>'s fields decode to: the glyph, sharp corners, and no seams where contours overlap.</summary>
[TestClass]
public class MsdfGeneratorTests
{
    private const string Printable = "!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~ÀÉÎõüßøæœŁ€";

    // Glyphs whose contours overlap in Inter's heaviest weight (found by summing each contour's
    // coverage signed by its winding: it exceeds one where filled contours overlap).
    private const string InterHeavyOverlaps = "#$+4ADKQRfkt{}ǾǪȺɆ";

    private static FontFace Inter => FontLibrary.Default.FindFace(FontLibrary.Inter)!;

    private static GlyphOutline Rectangle(float x0, float y0, float x1, float y1, bool clockwise = true)
    {
        var builder = new GlyphOutline.Builder();
        AddRectangle(builder, x0, y0, x1, y1, clockwise);
        return builder.Build();
    }

    private static void AddRectangle(GlyphOutline.Builder builder, float x0, float y0, float x1, float y1, bool clockwise = true)
    {
        // Clockwise with y up is how TrueType winds filled contours.
        builder.MoveTo(x0, y0);
        if (clockwise)
        {
            builder.LineTo(x0, y1);
            builder.LineTo(x1, y1);
            builder.LineTo(x1, y0);
        }
        else
        {
            builder.LineTo(x1, y0);
            builder.LineTo(x1, y1);
            builder.LineTo(x0, y1);
        }
        builder.Close();
    }

    /// <summary>Coverage (0 to 1) of the pixel at (x, y) from the pen, y down; 0 off the bitmap.</summary>
    private static float CoverageAt(GlyphBitmap coverage, int x, int y)
    {
        x -= coverage.Left;
        y -= coverage.Top;
        return x < 0 || y < 0 || x >= coverage.Width || y >= coverage.Height ? 0f : coverage[x, y] / 255f;
    }

    /// <summary>Whether every pixel within <paramref name="radius"/> (a square) of (x, y) is fully inside (1) or fully outside (-1), else 0.</summary>
    private static int Uniformity(GlyphBitmap coverage, int x, int y, int radius)
    {
        bool allIn = true, allOut = true;
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var c = CoverageAt(coverage, x + dx, y + dy);
                allIn &= c >= 0.999f;
                allOut &= c <= 0.001f;
            }
        }
        return allIn ? 1 : allOut ? -1 : 0;
    }

    private static float Median(MsdfBitmap bitmap, int x, int y)
    {
        var p = bitmap.Pixels.Slice((y * bitmap.Width + x) * 4, 3);
        return Math.Max(Math.Min(p[0], p[1]), Math.Min(Math.Max(p[0], p[1]), p[2])) / 255f;
    }

    /// <summary>
    /// The field sampled bilinearly, as a GPU samples it, at (x, y) in pixels from the pen: the
    /// median of the interpolated channels.
    /// </summary>
    private static float SampleMedian(MsdfBitmap bitmap, float x, float y)
    {
        Span<float> channels = stackalloc float[3];
        for (var c = 0; c < 3; c++)
        {
            channels[c] = Bilinear(bitmap.Width, bitmap.Height, (i, j) => bitmap.Pixels[(j * bitmap.Width + i) * 4 + c] / 255f, x - bitmap.Left, y - bitmap.Top);
        }
        return Math.Max(Math.Min(channels[0], channels[1]), Math.Min(Math.Max(channels[0], channels[1]), channels[2]));
    }

    private static float Bilinear(int width, int height, Func<int, int, float> texel, float u, float v)
    {
        u -= 0.5f;
        v -= 0.5f;
        var i = (int)MathF.Floor(u);
        var j = (int)MathF.Floor(v);
        float fu = u - i, fv = v - j;
        float At(int a, int b) => texel(Math.Clamp(a, 0, width - 1), Math.Clamp(b, 0, height - 1));
        return (1 - fv) * ((1 - fu) * At(i, j) + fu * At(i + 1, j)) + fv * ((1 - fu) * At(i, j + 1) + fu * At(i + 1, j + 1));
    }

    [TestMethod]
    public void AnEmptyOutlineGivesAnEmptyField()
    {
        Assert.IsTrue(MsdfGenerator.Generate(GlyphOutline.Empty, 1f, 4f).IsEmpty);
    }

    [TestMethod]
    public void TheFieldCoversTheGlyphWithHalfTheRangeAround()
    {
        var bitmap = MsdfGenerator.Generate(Rectangle(0, 0, 10, 10), 1f, 4f);

        Assert.AreEqual((-2, -12, 14, 14), (bitmap.Left, bitmap.Top, bitmap.Width, bitmap.Height));
        Assert.AreEqual(4f, bitmap.Range);
        // Deep inside, the distance is clamped to half the range.
        Assert.AreEqual(2f, bitmap.SignedDistance(7, 7), 0.01f);
        // Half a pixel in from the left edge, and a pixel and a half out from it.
        Assert.AreEqual(0.5f, bitmap.SignedDistance(2, 7), 0.02f);
        Assert.AreEqual(-1.5f, bitmap.SignedDistance(0, 7), 0.02f);
        // Diagonally out from the corner the median is the larger of the distances to the two
        // sides' extensions, not the distance to the corner (2.1): the corner stays square.
        Assert.AreEqual(-1.5f, bitmap.SignedDistance(0, 0), 0.02f);
    }

    [TestMethod]
    public void OutlinesWoundEitherWayGiveTheSameField()
    {
        // TrueType winds filled contours clockwise and CFF anticlockwise; both mean the same fill.
        var clockwise = MsdfGenerator.Generate(Rectangle(0.3f, 0.6f, 12.3f, 9.6f), 1f, 4f);
        var anticlockwise = MsdfGenerator.Generate(Rectangle(0.3f, 0.6f, 12.3f, 9.6f, clockwise: false), 1f, 4f);

        for (var y = 0; y < clockwise.Height; y++)
        {
            for (var x = 0; x < clockwise.Width; x++)
            {
                Assert.AreEqual(Median(clockwise, x, y), Median(anticlockwise, x, y), 1.5f / 255f, $"({x}, {y})");
            }
        }
    }

    [TestMethod]
    [DataRow(FontLibrary.Inter, 400f, 40f, 6f)]
    [DataRow(FontLibrary.Inter, 900f, 40f, 6f)]
    [DataRow(FontLibrary.Inter, 100f, 24f, 4f)]
    [DataRow(FontLibrary.JetBrainsMono, 400f, 32f, 4f)]
    public void InsideAndOutsideAgreeWithCoverageAwayFromEdges(string family, float weight, float size, float range)
    {
        var font = FontLibrary.Default.FindFace(family)!.Instance(new FontVariation(FontVariation.Weight, weight));
        var tested = 0;
        var disagreements = new List<string>();
        foreach (var rune in Printable.EnumerateRunes())
        {
            if (!font.TryGetGlyph(rune.Value, out var glyph))
            {
                continue;
            }
            var outline = font.GetOutline(glyph);
            var coverage = GlyphRasterizer.Rasterize(outline, font.Scale(size));
            var msdf = MsdfGenerator.Generate(outline, font.Scale(size), range);
            for (var y = 0; y < msdf.Height; y++)
            {
                for (var x = 0; x < msdf.Width; x++)
                {
                    // Pixels whose 3 × 3 neighbourhood is all in or all out are over a pixel from an edge.
                    var expected = Uniformity(coverage, msdf.Left + x, msdf.Top + y, 1);
                    if (expected == 0)
                    {
                        continue;
                    }
                    tested++;
                    var inside = Median(msdf, x, y) > 0.5f;
                    if (inside != expected > 0)
                    {
                        disagreements.Add($"{rune} ({x}, {y})");
                    }
                }
            }
        }

        Assert.IsTrue(tested > 10_000, $"{tested} pixels tested");
        Assert.AreEqual(0, disagreements.Count, string.Join(", ", disagreements));
    }

    [TestMethod]
    public void ContoursThatOverlapLeaveNoSeam()
    {
        // A plus of two bars wound the same way: each bar's edges inside the other are not borders.
        var builder = new GlyphOutline.Builder();
        AddRectangle(builder, 0, 10, 30, 20);
        AddRectangle(builder, 10, 0, 20, 30);
        var bitmap = MsdfGenerator.Generate(builder.Build(), 1f, 4f);

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                // The texel's centre, in font units (y up).
                var px = bitmap.Left + x + 0.5f;
                var py = -(bitmap.Top + y + 0.5f);
                // Inside the union, the distance to its outline is at least the distance to the
                // outline of whichever bar the point is in.
                var inHorizontal = MathF.Min(MathF.Min(px, 30 - px), MathF.Min(py - 10, 20 - py));
                var inVertical = MathF.Min(MathF.Min(px - 10, 20 - px), MathF.Min(py, 30 - py));
                var least = MathF.Max(inHorizontal, inVertical);
                if (least > 0)
                {
                    Assert.IsTrue(
                        bitmap.SignedDistance(x, y) >= MathF.Min(least, 2f) - 0.25f,
                        $"({px}, {py}) decodes to {bitmap.SignedDistance(x, y)}, but is {least} inside");
                }
            }
        }
    }

    [TestMethod]
    public void OverlappingContoursOfHeavyGlyphsLeaveNoSeam()
    {
        var font = Inter.Instance(new FontVariation(FontVariation.Weight, 900));
        var tested = 0;
        foreach (var rune in InterHeavyOverlaps.EnumerateRunes())
        {
            Assert.IsTrue(font.TryGetGlyph(rune.Value, out var glyph), rune.ToString());
            var outline = font.GetOutline(glyph);
            const float size = 48f, range = 4f;
            var coverage = GlyphRasterizer.Rasterize(outline, font.Scale(size));
            var msdf = MsdfGenerator.Generate(outline, font.Scale(size), range);
            for (var y = 0; y + 1 < msdf.Height; y++)
            {
                for (var x = 0; x + 1 < msdf.Width; x++)
                {
                    // A block of 2 × 2 texels over a pixel inside the filled area: the field
                    // interpolated anywhere between them, magnified 4 ×, must stay inside. A seam
                    // (an edge of one contour inside another taken for a border) would dip below.
                    int px = msdf.Left + x, py = msdf.Top + y;
                    if (Uniformity(coverage, px, py, 1) <= 0 || Uniformity(coverage, px + 1, py + 1, 1) <= 0
                        || Uniformity(coverage, px + 1, py, 1) <= 0 || Uniformity(coverage, px, py + 1, 1) <= 0)
                    {
                        continue;
                    }
                    tested++;
                    for (var j = 0; j <= 4; j++)
                    {
                        for (var i = 0; i <= 4; i++)
                        {
                            var sx = px + 0.5f + i / 4f;
                            var sy = py + 0.5f + j / 4f;
                            Assert.IsTrue(SampleMedian(msdf, sx, sy) > 0.5f, $"{rune} at ({sx}, {sy}) is inside but samples {SampleMedian(msdf, sx, sy)}");
                        }
                    }
                }
            }
        }
        Assert.IsTrue(tested > 3000, $"{tested} blocks tested");
    }

    [TestMethod]
    public void ACornerMagnifiedStaysSharpWhereASingleChannelFieldRoundsIt()
    {
        // A square, its top-right corner at (14.3, 14.6) font units = pixels, off the texel grid.
        const float right = 14.3f, top = 14.6f;
        var msdf = MsdfGenerator.Generate(Rectangle(2.3f, 2.6f, right, top), 1f, 4f);

        // The same square's true signed distance, at the same texels and quantized the same way:
        // what a single-channel SDF of it holds.
        float TrueDistance(float x, float yUp)
        {
            float dx = MathF.Max(2.3f - x, x - right), dy = MathF.Max(2.6f - yUp, yUp - top);
            return dx <= 0 && dy <= 0 ? -MathF.Max(dx, dy) : -MathF.Sqrt(MathF.Pow(MathF.Max(dx, 0), 2) + MathF.Pow(MathF.Max(dy, 0), 2));
        }
        float SdfTexel(int i, int j)
        {
            var value = Math.Clamp(TrueDistance(msdf.Left + i + 0.5f, -(msdf.Top + j + 0.5f)) / msdf.Range + 0.5f, 0f, 1f);
            return MathF.Round(value * 255f) / 255f;
        }

        // Sample the pixel square inside the corner at 8 × 8 points a pixel, as a magnified draw would.
        int msdfWrong = 0, sdfWrong = 0;
        for (var j = 0; j < 8; j++)
        {
            for (var i = 0; i < 8; i++)
            {
                var x = right - 1f + (i + 0.5f) / 8f;
                var y = -top + (j + 0.5f) / 8f; // y down: from the top edge into the square
                msdfWrong += SampleMedian(msdf, x, y) > 0.5f ? 0 : 1;
                sdfWrong += Bilinear(msdf.Width, msdf.Height, SdfTexel, x - msdf.Left, y - msdf.Top) > 0.5f ? 0 : 1;
            }
        }

        Assert.AreEqual(0, msdfWrong, "samples inside the corner the MSDF puts outside");
        Assert.IsTrue(sdfWrong >= 4, $"the single-channel field rounds the corner off: {sdfWrong} of 64 samples outside");
    }
}
