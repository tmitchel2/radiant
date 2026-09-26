using System;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Text.Tests;

[TestClass]
public class GlyphRasterizerTests
{
    // Font units are pixels at scale 1; y is up, so a square from y=0 to y=2 sits above the baseline.
    private static GlyphOutline Rectangle(float x0, float y0, float x1, float y1, bool clockwise = false)
    {
        var builder = new GlyphOutline.Builder();
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
        return builder.Build();
    }

    private static float Total(GlyphBitmap bitmap)
    {
        var total = 0f;
        foreach (var value in bitmap.Coverage)
        {
            total += value / 255f;
        }
        return total;
    }

    [TestMethod]
    public void APixelAlignedSquareFillsItsPixelsExactly()
    {
        var bitmap = GlyphRasterizer.Rasterize(Rectangle(0, 0, 2, 2), 1f);

        Assert.AreEqual((0, -2, 2, 2), (bitmap.Left, bitmap.Top, bitmap.Width, bitmap.Height));
        CollectionAssert.AreEqual(new byte[] { 255, 255, 255, 255 }, bitmap.Coverage.ToArray());
    }

    [TestMethod]
    public void AnEdgeHalfwayAcrossAPixelHalfCoversIt()
    {
        var bitmap = GlyphRasterizer.Rasterize(Rectangle(0, 0, 2, 1), 1f, new Vector2(0.5f, 0));

        Assert.AreEqual(3, bitmap.Width);
        CollectionAssert.AreEqual(new byte[] { 128, 255, 128 }, bitmap.Coverage.ToArray());
    }

    [TestMethod]
    public void CoverageAddsUpToTheArea()
    {
        var builder = new GlyphOutline.Builder();
        builder.MoveTo(0, 0);
        builder.LineTo(10, 0);
        builder.LineTo(3, 7);
        builder.Close();

        var bitmap = GlyphRasterizer.Rasterize(builder.Build(), 1f, new Vector2(0.3f, 0.6f));

        Assert.AreEqual(35f, Total(bitmap), 0.2f);
    }

    [TestMethod]
    public void CurvesAreCoveredToTheirArea()
    {
        // A circle of radius 8 from four cubic quarter arcs (the usual 0.5523 approximation).
        const float r = 8f, k = 0.5523f * r;
        var builder = new GlyphOutline.Builder();
        builder.MoveTo(r, 0);
        builder.CubicTo(r, k, k, r, 0, r);
        builder.CubicTo(-k, r, -r, k, -r, 0);
        builder.CubicTo(-r, -k, -k, -r, 0, -r);
        builder.CubicTo(k, -r, r, -k, r, 0);
        builder.Close();

        var bitmap = GlyphRasterizer.Rasterize(builder.Build(), 1f);

        // Flattened, the curve's chords sit inside it (by up to the 0.05 px tolerance), so a
        // little area is lost along the 50 px perimeter: within 1%.
        Assert.AreEqual(MathF.PI * r * r, Total(bitmap), 0.01f * MathF.PI * r * r);
    }

    [TestMethod]
    public void OverlappingContoursWoundTheSameWayDoNotDoubleUp()
    {
        var builder = new GlyphOutline.Builder();
        foreach (var (x0, x1) in new[] { (0f, 3f), (1f, 4f) })
        {
            builder.MoveTo(x0, 0);
            builder.LineTo(x1, 0);
            builder.LineTo(x1, 1);
            builder.LineTo(x0, 1);
            builder.Close();
        }

        var bitmap = GlyphRasterizer.Rasterize(builder.Build(), 1f);

        CollectionAssert.AreEqual(new byte[] { 255, 255, 255, 255 }, bitmap.Coverage.ToArray());
    }

    [TestMethod]
    public void AContourWoundTheOtherWayCutsAHole()
    {
        var outer = Rectangle(0, 0, 3, 3);
        var inner = Rectangle(1, 1, 2, 2, clockwise: true);
        var builder = new GlyphOutline.Builder();
        foreach (var outline in new[] { outer, inner })
        {
            var p = 0;
            foreach (var verb in outline.Verbs)
            {
                switch (verb)
                {
                    case PathVerb.MoveTo: builder.MoveTo(outline.Points[p].X, outline.Points[p++].Y); break;
                    case PathVerb.LineTo: builder.LineTo(outline.Points[p].X, outline.Points[p++].Y); break;
                    case PathVerb.Close: builder.Close(); break;
                }
            }
        }

        var bitmap = GlyphRasterizer.Rasterize(builder.Build(), 1f);

        Assert.AreEqual(0, bitmap[1, 1]);
        Assert.AreEqual(8f, Total(bitmap), 1e-3f);
    }

    [TestMethod]
    public void AContourNeedNotBeClosedExplicitly()
    {
        var builder = new GlyphOutline.Builder();
        builder.MoveTo(0, 0);
        builder.LineTo(2, 0);
        builder.LineTo(2, 2);
        builder.LineTo(0, 2);

        var bitmap = GlyphRasterizer.Rasterize(builder.Build(), 1f);

        Assert.AreEqual(4f, Total(bitmap), 1e-3f);
    }

    [TestMethod]
    public void AQuarterPixelOffsetMovesTheGlyphAQuarterPixel()
    {
        var o = FontLibrary.Default.Resolve(FontLibrary.Inter, FontWeight.Regular, italic: false, 16);
        o.TryGetGlyph('o', out var glyph);
        var outline = o.GetOutline(glyph);
        var scale = o.Scale(16);

        var at0 = GlyphRasterizer.Rasterize(outline, scale);
        var atQuarter = GlyphRasterizer.Rasterize(outline, scale, new Vector2(0.25f, 0));

        Assert.AreEqual(Centroid(at0) + 0.25f, Centroid(atQuarter), 0.02f);
        Assert.AreEqual(Total(at0), Total(atQuarter), 0.05f * Total(at0), "the same ink either way");
    }

    [TestMethod]
    public void ARealGlyphHasItsCounterOpen()
    {
        var inter = FontLibrary.Default.Resolve(FontLibrary.Inter, FontWeight.Regular, italic: false, 48);
        inter.TryGetGlyph('o', out var glyph);

        var bitmap = GlyphRasterizer.Rasterize(inter.GetOutline(glyph), inter.Scale(48));

        Assert.AreEqual(0, bitmap[bitmap.Width / 2, bitmap.Height / 2], "the middle of an o is empty");
        Assert.AreEqual(255, bitmap[bitmap.Width / 2, 1], "its top stroke is solid");
        Assert.IsTrue(bitmap.Top < 0 && bitmap.Top + bitmap.Height > 0, "it sits on the baseline");
    }

    [TestMethod]
    public void AnEmptyGlyphHasNoPixels()
    {
        var inter = FontLibrary.Default.Resolve(FontLibrary.Inter, FontWeight.Regular, italic: false, 16);
        inter.TryGetGlyph(' ', out var space);

        Assert.IsTrue(GlyphRasterizer.Rasterize(inter.GetOutline(space), inter.Scale(16)).IsEmpty);
    }

    private static float Centroid(GlyphBitmap bitmap)
    {
        float sum = 0f, weighted = 0f;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                sum += bitmap[x, y];
                weighted += bitmap[x, y] * (bitmap.Left + x + 0.5f);
            }
        }
        return weighted / sum;
    }
}
