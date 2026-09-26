using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Text.Slug;

namespace Radiant.Text.Tests;

/// <summary>
/// The Slug coverage calculation, run on the CPU exactly as the shader runs it, against shapes with
/// known coverage and against <see cref="GlyphRasterizer"/>'s exact-area coverage of real glyphs.
/// </summary>
[TestClass]
public class SlugCoverageTests
{
    private const string Printable = "!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

    private static SlugGlyph Build(params (float X0, float Y0, float X1, float Y1, bool Clockwise)[] rectangles)
    {
        // Em units given directly: a unit per em.
        var builder = new GlyphOutline.Builder();
        foreach (var (x0, y0, x1, y1, clockwise) in rectangles)
        {
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
        return SlugGlyphBuilder.Build(builder.Build(), 1);
    }

    [TestMethod]
    public void RootEligibilityIsThePapersTable()
    {
        // Table 1: by whether y1, y2, y3 are positive, whether the first root (adds) and the second
        // root (subtracts) count.
        (bool Y1, bool Y2, bool Y3, bool T1, bool T2)[] table =
        [
            (false, false, false, false, false), // A
            (true, false, false, true, false),   // B
            (false, true, false, true, true),    // C
            (true, true, false, true, false),    // D
            (false, false, true, false, true),   // E
            (true, false, true, true, true),     // F
            (false, true, true, false, true),    // G
            (true, true, true, false, false),    // H
        ];
        foreach (var (y1, y2, y3, t1, t2) in table)
        {
            var code = SlugCoverage.RootCode(y1 ? 1f : -1f, y2 ? 1f : -1f, y3 ? 1f : -1f);
            Assert.AreEqual(t1, (code & 1) != 0, $"{y1} {y2} {y3}: t1");
            Assert.AreEqual(t2, (code & 0x100) != 0, $"{y1} {y2} {y3}: t2");
        }
    }

    [TestMethod]
    public void InsideIsCoveredOutsideIsNotAndAnEdgeThroughTheCentreIsHalf()
    {
        // A 10 × 10 pixel square at 100 pixels per em.
        var glyph = Build((0, 0, 0.1f, 0.1f, false));
        var ppe = new Vector2(100);

        Assert.AreEqual(1f, SlugCoverage.Evaluate(glyph, new(0.05f, 0.05f), ppe), 1e-5f);
        Assert.AreEqual(0f, SlugCoverage.Evaluate(glyph, new(0.2f, 0.05f), ppe), 1e-5f);
        Assert.AreEqual(0f, SlugCoverage.Evaluate(glyph, new(0.05f, -0.05f), ppe), 1e-5f);
        Assert.AreEqual(0.5f, SlugCoverage.Evaluate(glyph, new(0f, 0.05f), ppe), 1e-4f);
        Assert.AreEqual(0.5f, SlugCoverage.Evaluate(glyph, new(0.05f, 0.1f), ppe), 1e-4f);
        Assert.AreEqual(0.25f, SlugCoverage.Evaluate(glyph, new(0.1025f, 0.05f), ppe), 1e-4f, "a quarter of the pixel inside");
    }

    [TestMethod]
    public void EitherWindingFills()
    {
        var clockwise = Build((0, 0, 0.1f, 0.1f, true));
        var anticlockwise = Build((0, 0, 0.1f, 0.1f, false));
        var ppe = new Vector2(100);

        for (var x = -0.02f; x < 0.12f; x += 0.0037f)
        {
            var sample = new Vector2(x, 0.0513f);
            Assert.AreEqual(SlugCoverage.Evaluate(anticlockwise, sample, ppe), SlugCoverage.Evaluate(clockwise, sample, ppe), 1e-6f);
        }
    }

    [TestMethod]
    public void OverlappingContoursFillOnceAndHolesStayOpen()
    {
        // Two overlapping squares wound the same way (non-zero: the overlap is inside once), and a
        // square with a hole wound the other way.
        var overlapping = Build((0, 0, 0.2f, 0.2f, false), (0.1f, 0.1f, 0.3f, 0.3f, false));
        var holed = Build((0, 0, 0.3f, 0.3f, false), (0.1f, 0.1f, 0.2f, 0.2f, true));
        var ppe = new Vector2(100);

        Assert.AreEqual(1f, SlugCoverage.Evaluate(overlapping, new(0.15f, 0.15f), ppe), 1e-5f);
        Assert.AreEqual(1f, SlugCoverage.Evaluate(overlapping, new(0.1f, 0.15f), ppe), 1e-5f, "an inner edge draws no seam");
        Assert.AreEqual(0f, SlugCoverage.Evaluate(holed, new(0.15f, 0.15f), ppe), 1e-5f);
        Assert.AreEqual(0.5f, SlugCoverage.Evaluate(holed, new(0.1f, 0.15f), ppe), 1e-4f);
    }

    [TestMethod]
    public void EveryPixelIsTheSameWhateverTheContourStartsAt()
    {
        // The historically hard case: rays through the shared end points of curves. A diamond has
        // its vertices on pixel-centre rows and columns.
        var builder = new GlyphOutline.Builder();
        builder.MoveTo(0, 0.1f);
        builder.LineTo(0.1f, 0);
        builder.LineTo(0.2f, 0.1f);
        builder.LineTo(0.1f, 0.2f);
        builder.Close();
        var glyph = SlugGlyphBuilder.Build(builder.Build(), 1);
        var ppe = new Vector2(100);

        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                var sample = new Vector2(x + 0.5f, y + 0.5f) / 100f;
                var inside = MathF.Abs(sample.X - 0.1f) + MathF.Abs(sample.Y - 0.1f) < 0.1f - 0.011f;
                var outside = MathF.Abs(sample.X - 0.1f) + MathF.Abs(sample.Y - 0.1f) > 0.1f + 0.011f;
                var coverage = SlugCoverage.Evaluate(glyph, sample, ppe);
                if (inside)
                {
                    Assert.AreEqual(1f, coverage, 1e-5f, $"({x}, {y})");
                }
                if (outside)
                {
                    Assert.AreEqual(0f, coverage, 1e-5f, $"({x}, {y})");
                }
            }
        }
        // Rays exactly through the vertices.
        Assert.AreEqual(1f, SlugCoverage.Evaluate(glyph, new(0.1f, 0.1f), ppe), 1e-5f);
        Assert.AreEqual(0f, SlugCoverage.Evaluate(glyph, new(0.1f, 0.25f), ppe), 1e-5f);
        Assert.AreEqual(0f, SlugCoverage.Evaluate(glyph, new(-0.1f, 0.1f), ppe), 1e-5f);
    }

    [TestMethod]
    public void InterGlyphsMatchExactAreaCoverage()
    {
        var font = FontLibrary.Default.Resolve(FontLibrary.Inter, FontWeight.Regular, italic: false, size: 16);
        var stats = Compare(font, [11f, 16f, 24f, 48f, 96f]);

        // Slug's two rays approximate area coverage: exact for straight edges crossing a pixel's
        // sides, close elsewhere, loosest for features under two pixels (a period at 11 px).
        Assert.IsTrue(stats.MeanError < 0.01f, stats.ToString());
        Assert.IsTrue(stats.WorstGlyphMeanError < 0.15f, stats.ToString());
        Assert.IsTrue(MathF.Abs(stats.InkRatio - 1f) < 0.01f, stats.ToString());
        Assert.AreEqual(0, stats.OutsideInk, stats.ToString());
        Assert.AreEqual(0, stats.Holes, stats.ToString());
    }

    [TestMethod]
    public void HeavyInterWithOverlappingContoursMatchesExactAreaCoverage()
    {
        var font = FontLibrary.Default.Resolve(FontLibrary.Inter, FontWeight.Black, italic: false, size: 32);
        var overlapping = Printable.Where(c => HasOverlap(font, c)).ToArray();
        Assert.IsTrue(overlapping.Length > 5, $"only {new string(overlapping)} overlap");

        var stats = Compare(font, [16f, 32f, 64f], new string(overlapping));

        Assert.IsTrue(stats.MeanError < 0.01f, stats.ToString());
        Assert.IsTrue(stats.WorstGlyphMeanError < 0.15f, stats.ToString());
        Assert.IsTrue(MathF.Abs(stats.InkRatio - 1f) < 0.01f, stats.ToString());
        Assert.AreEqual(0, stats.OutsideInk, stats.ToString());
        Assert.AreEqual(0, stats.Holes, $"{stats}: a pixel deep inside the ink came out less than full");
    }

    private readonly record struct Stats(
        float MeanError, float WorstGlyphMeanError, string WorstGlyph, float MaxError, float InkRatio, int OutsideInk, int Holes);

    /// <summary>
    /// Slug coverage against GlyphRasterizer's for each glyph at each size, over the rasterizer's
    /// box plus a ring of pixels around it (which must stay empty).
    /// </summary>
    private static Stats Compare(FontInstance font, float[] sizes, string text = Printable)
    {
        double errorSum = 0, slugInk = 0, exactInk = 0;
        var pixels = 0;
        var worstGlyphError = 0f;
        var worstGlyph = "";
        var maxError = 0f;
        var outside = 0;
        var holes = 0;
        var offsets = new[] { Vector2.Zero, new Vector2(0.25f, 0.5f), new Vector2(0.7f, 0.3f) };
        foreach (var c in text)
        {
            Assert.IsTrue(font.TryGetGlyph(c, out var id));
            var outline = font.GetOutline(id);
            var glyph = SlugGlyphBuilder.Build(outline, font.Face.UnitsPerEm);
            foreach (var size in sizes)
            {
                foreach (var offset in offsets)
                {
                    var exact = GlyphRasterizer.Rasterize(outline, font.Scale(size), offset);
                    var ppe = new Vector2(size);
                    double glyphError = 0;
                    for (var y = -1; y <= exact.Height; y++)
                    {
                        for (var x = -1; x <= exact.Width; x++)
                        {
                            var centre = new Vector2(exact.Left + x + 0.5f, exact.Top + y + 0.5f);
                            var em = new Vector2(centre.X - offset.X, offset.Y - centre.Y) / size;
                            var slug = SlugCoverage.Evaluate(glyph, em, ppe);
                            if (x < 0 || y < 0 || x == exact.Width || y == exact.Height)
                            {
                                outside += slug > 0.5f / 255f ? 1 : 0;
                                continue;
                            }
                            var reference = exact[x, y] / 255f;
                            var error = MathF.Abs(slug - reference);
                            holes += Interior(exact, x, y) && slug < 1f ? 1 : 0;
                            glyphError += error;
                            errorSum += error;
                            maxError = MathF.Max(maxError, error);
                            slugInk += slug;
                            exactInk += reference;
                            pixels++;
                        }
                    }
                    var mean = (float)(glyphError / Math.Max(1, exact.Width * exact.Height));
                    if (mean > worstGlyphError)
                    {
                        worstGlyphError = mean;
                        worstGlyph = $"'{c}' at {size}px";
                    }
                }
            }
        }
        return new Stats((float)(errorSum / pixels), worstGlyphError, worstGlyph, maxError, (float)(slugInk / exactInk), outside, holes);
    }

    // Whether a pixel and all eight of its neighbours are fully covered: deep inside the ink, where
    // Slug must give exactly one (a seam where contours overlap would show as less).
    private static bool Interior(GlyphBitmap bitmap, int x, int y)
    {
        if (x < 1 || y < 1 || x + 1 >= bitmap.Width || y + 1 >= bitmap.Height)
        {
            return false;
        }
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (bitmap[x + dx, y + dy] != 255)
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Whether some point of a glyph is inside two of its contours at once (winding number two or
    /// more), found independently of Slug: contours flattened, winding counted by crossings.
    /// </summary>
    private static bool HasOverlap(FontInstance font, char c)
    {
        Assert.IsTrue(font.TryGetGlyph(c, out var id));
        var contours = SlugGlyphBuilder.ToQuadratics(font.GetOutline(id), font.Face.UnitsPerEm);
        var polygon = new List<(Vector2 A, Vector2 B)>();
        foreach (var curve in contours.SelectMany(contour => contour))
        {
            for (var i = 0; i < 16; i++)
            {
                polygon.Add((curve.Evaluate(i / 16f), curve.Evaluate((i + 1) / 16f)));
            }
        }
        if (polygon.Count == 0)
        {
            return false;
        }
        var min = polygon.Aggregate(new Vector2(float.MaxValue), (m, e) => Vector2.Min(m, Vector2.Min(e.A, e.B)));
        var max = polygon.Aggregate(new Vector2(float.MinValue), (m, e) => Vector2.Max(m, Vector2.Max(e.A, e.B)));
        for (var j = 0; j < 64; j++)
        {
            for (var i = 0; i < 64; i++)
            {
                var p = min + ((max - min) * new Vector2((i + 0.5f) / 64f, (j + 0.5f) / 64f));
                var winding = 0;
                foreach (var (a, b) in polygon)
                {
                    if ((a.Y <= p.Y) != (b.Y <= p.Y) && a.X + ((p.Y - a.Y) / (b.Y - a.Y) * (b.X - a.X)) > p.X)
                    {
                        winding += b.Y > a.Y ? 1 : -1;
                    }
                }
                if (Math.Abs(winding) >= 2)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
