using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Text.Slug;

namespace Radiant.Text.Tests;

[TestClass]
public class SlugGlyphBuilderTests
{
    // Font units with 100 to the em, so a unit is 0.01 em.
    private const int UnitsPerEm = 100;

    private static GlyphOutline Square(float x0, float y0, float x1, float y1, bool close = true)
    {
        var builder = new GlyphOutline.Builder();
        builder.MoveTo(x0, y0);
        builder.LineTo(x1, y0);
        builder.LineTo(x1, y1);
        builder.LineTo(x0, y1);
        if (close)
        {
            builder.Close();
        }
        return builder.Build();
    }

    private static FontInstance Inter(float weight = FontWeight.Regular) =>
        FontLibrary.Default.Resolve(FontLibrary.Inter, weight, italic: false, size: 16);

    private static SlugGlyph InterGlyph(char c, float weight = FontWeight.Regular)
    {
        var font = Inter(weight);
        Assert.IsTrue(font.TryGetGlyph(c, out var glyph));
        return SlugGlyphBuilder.Build(font.GetOutline(glyph), font.Face.UnitsPerEm);
    }

    [TestMethod]
    public void LinesBecomeQuadraticsWithTheControlPointOnTheEnd()
    {
        var contours = SlugGlyphBuilder.ToQuadratics(Square(0, 0, 10, 20), UnitsPerEm);

        Assert.AreEqual(1, contours.Count);
        Assert.AreEqual(4, contours[0].Count);
        Assert.AreEqual(new SlugCurve(new(0, 0), new(0.1f, 0), new(0.1f, 0)), contours[0][0]);
        foreach (var curve in contours[0])
        {
            Assert.AreEqual(curve.P3, curve.P2);
        }
    }

    [TestMethod]
    public void ContoursChainAndCloseWhetherOrNotTheOutlineClosesThem()
    {
        foreach (var close in new[] { true, false })
        {
            var contour = SlugGlyphBuilder.ToQuadratics(Square(0, 0, 10, 20, close), UnitsPerEm).Single();

            Assert.AreEqual(4, contour.Count, $"close: {close}");
            for (var i = 0; i < contour.Count; i++)
            {
                Assert.AreEqual(contour[i].P3, contour[(i + 1) % contour.Count].P1, $"close: {close}, curve {i}");
            }
        }
    }

    [TestMethod]
    public void ZeroLengthSegmentsAreDropped()
    {
        var builder = new GlyphOutline.Builder();
        builder.MoveTo(0, 0);
        builder.LineTo(0, 0);
        builder.LineTo(10, 0);
        builder.QuadTo(10, 0, 10, 0);
        builder.LineTo(10, 10);
        builder.LineTo(0, 0);
        builder.Close();

        var contour = SlugGlyphBuilder.ToQuadratics(builder.Build(), UnitsPerEm).Single();

        Assert.AreEqual(3, contour.Count);
    }

    [TestMethod]
    public void CubicsAreApproximatedWithinTheTolerance()
    {
        // A quarter circle of radius 1 em, and an S-bend with an inflection.
        (Vector2, Vector2, Vector2, Vector2)[] cubics =
        [
            (new(1, 0), new(1, 0.5523f), new(0.5523f, 1), new(0, 1)),
            (new(0, 0), new(1.2f, 0.1f), new(-0.4f, 0.9f), new(0.8f, 1)),
        ];
        foreach (var (p0, c1, c2, p3) in cubics)
        {
            var pieces = new List<SlugCurve>();
            SlugGlyphBuilder.AddCubic(pieces, p0, c1, c2, p3, SlugGlyphBuilder.CubicTolerance);

            Assert.IsTrue(pieces.Count > 1, "one quadratic can't follow a quarter circle to 1/4096 em");
            Assert.AreEqual(p0, pieces[0].P1);
            Assert.AreEqual(p3, pieces[^1].P3);
            var worst = 0f;
            for (var i = 0; i < pieces.Count; i++)
            {
                if (i > 0)
                {
                    Assert.AreEqual(pieces[i - 1].P3, pieces[i].P1, "pieces chain exactly");
                }
                for (var s = 0; s <= 32; s++)
                {
                    // Each piece spans an equal share of the cubic's parameter.
                    var t = (i + (s / 32f)) / pieces.Count;
                    var u = 1f - t;
                    var onCubic = (u * u * u * p0) + (3 * u * u * t * c1) + (3 * u * t * t * c2) + (t * t * t * p3);
                    worst = MathF.Max(worst, Vector2.Distance(onCubic, pieces[i].Evaluate(s / 32f)));
                }
            }
            Assert.IsTrue(worst <= SlugGlyphBuilder.CubicTolerance * 1.01f, $"{worst * 4096f} / 4096 em from the cubic");
        }
    }

    [TestMethod]
    public void CurvesArePackedSoEachSharesItsEndTexelWithTheNext()
    {
        var glyph = SlugGlyphBuilder.Build(Square(0, 0, 10, 20), UnitsPerEm);

        // Four curves and one closing texel.
        Assert.AreEqual(4, glyph.CurveCount);
        Assert.AreEqual(5, glyph.Curves.Length);
        Assert.AreEqual(new Vector4(0, 0, 0.1f, 0), glyph.Curves[0]);
        Assert.AreEqual(new Vector4(0.1f, 0, 0.1f, 0.2f), glyph.Curves[1], "the second curve starts where the first ends");
        Assert.AreEqual(new Vector4(0, 0, 0, 0), glyph.Curves[4]);
        Assert.AreEqual(new Vector2(0, 0), glyph.Min);
        Assert.AreEqual(new Vector2(0.1f, 0.2f), glyph.Max);
    }

    [TestMethod]
    public void CurvesThatDoNotChainGetAnEndTexelEach()
    {
        // Two triangles given as one list of curves, the second not starting where the first ends.
        static SlugCurve Line(float x0, float y0, float x1, float y1) => new(new(x0, y0), new(x1, y1), new(x1, y1));
        IReadOnlyList<SlugCurve> curves =
        [
            Line(0, 0, 1, 0), Line(1, 0, 0, 1), Line(0, 1, 0, 0),
            Line(2, 0, 3, 0), Line(3, 0, 2, 1), Line(2, 1, 2, 0),
        ];

        var glyph = SlugGlyphBuilder.Build([curves]);

        Assert.AreEqual(6, glyph.CurveCount);
        Assert.AreEqual(8, glyph.Curves.Length, "six curves and two end texels");
        Assert.AreEqual(new Vector4(0, 0, 0, 0), glyph.Curves[3], "the first triangle's end point");
        Assert.AreEqual(new Vector4(2, 0, 3, 0), glyph.Curves[4], "the second triangle's first curve");
        Assert.AreEqual(1f, SlugCoverage.Evaluate(glyph, new(2.2f, 0.2f), new(100)), 1e-5f);
        Assert.AreEqual(1f, SlugCoverage.Evaluate(glyph, new(0.2f, 0.2f), new(100)), 1e-5f);
        Assert.AreEqual(0f, SlugCoverage.Evaluate(glyph, new(1.5f, 0.2f), new(100)), 1e-5f);
    }

    [TestMethod]
    public void ARectangleNeedsOneBandEachWayAndLeavesOutLinesParallelToTheRays()
    {
        var glyph = SlugGlyphBuilder.Build(Square(0, 0, 10, 20), UnitsPerEm);

        Assert.AreEqual(1, glyph.HorizontalBandCount);
        Assert.AreEqual(1, glyph.VerticalBandCount);
        // The horizontal band has only the two vertical sides; the vertical band, the two horizontal.
        Assert.AreEqual(2, (int)glyph.Bands[SlugGlyph.HeaderLength]);
        Assert.AreEqual(2, (int)glyph.Bands[SlugGlyph.HeaderLength + 2]);
        Assert.AreEqual(2, glyph.MaxCurvesPerBand);
    }

    [TestMethod]
    public void BandsHoldEveryCurveThatCrossesThemSortedByGreatestCoordinate()
    {
        foreach (var c in "aegs@&%8BRW")
        {
            var glyph = InterGlyph(c);
            var curves = Curves(glyph);
            Assert.AreEqual(glyph.CurveCount, curves.Count, $"'{c}': every curve is in some band");
            Assert.IsTrue(glyph.HorizontalBandCount is > 1 and <= SlugGlyphBuilder.MaxBands, $"'{c}': {glyph.HorizontalBandCount} horizontal bands");
            Assert.IsTrue(glyph.VerticalBandCount is > 1 and <= SlugGlyphBuilder.MaxBands, $"'{c}': {glyph.VerticalBandCount} vertical bands");
            Assert.IsTrue(glyph.MaxCurvesPerBand < glyph.CurveCount, $"'{c}': banding leaves a pixel fewer curves to visit");

            foreach (var vertical in new[] { false, true })
            {
                var count = vertical ? glyph.VerticalBandCount : glyph.HorizontalBandCount;
                var min = vertical ? glyph.Min.X : glyph.Min.Y;
                var max = vertical ? glyph.Max.X : glyph.Max.Y;
                for (var band = 0; band < count; band++)
                {
                    var listed = Band(glyph, vertical, band);
                    var greatest = listed.Select(at => vertical ? curves[at].Max.Y : curves[at].Max.X).ToList();
                    CollectionAssert.AreEqual(greatest.OrderByDescending(g => g).ToList(), greatest, $"'{c}' band {band} is sorted");

                    // Every curve that reaches into the band and isn't parallel to its rays is listed.
                    var lo = min + ((max - min) * band / count);
                    var hi = min + ((max - min) * (band + 1) / count);
                    foreach (var (at, curve) in curves)
                    {
                        var cMin = vertical ? curve.Min.X : curve.Min.Y;
                        var cMax = vertical ? curve.Max.X : curve.Max.Y;
                        var parallel = cMin == cMax;
                        if (!parallel && cMax >= lo && cMin <= hi)
                        {
                            CollectionAssert.Contains(listed, at, $"'{c}' {(vertical ? "vertical" : "horizontal")} band {band}");
                        }
                        if (parallel)
                        {
                            CollectionAssert.DoesNotContain(listed, at);
                        }
                    }
                }
            }
        }
    }

    [TestMethod]
    public void AnEmptyOutlineMakesAnEmptyGlyph()
    {
        var glyph = SlugGlyphBuilder.Build(GlyphOutline.Empty, UnitsPerEm);

        Assert.IsTrue(glyph.IsEmpty);
        Assert.AreSame(SlugGlyph.Empty, glyph);
    }

    // The curves of a glyph by the texel they start at.
    private static Dictionary<int, SlugCurve> Curves(SlugGlyph glyph)
    {
        var all = new Dictionary<int, SlugCurve>();
        var bands = glyph.Bands;
        var lists = glyph.HorizontalBandCount + glyph.VerticalBandCount;
        for (var b = 0; b < lists; b++)
        {
            var count = (int)bands[SlugGlyph.HeaderLength + (2 * b)];
            var offset = (int)bands[SlugGlyph.HeaderLength + (2 * b) + 1];
            for (var i = 0; i < count; i++)
            {
                var at = (int)bands[offset + i];
                var t0 = glyph.Curves[at];
                var t1 = glyph.Curves[at + 1];
                all[at] = new SlugCurve(new(t0.X, t0.Y), new(t0.Z, t0.W), new(t1.X, t1.Y));
            }
        }
        return all;
    }

    private static List<int> Band(SlugGlyph glyph, bool vertical, int band)
    {
        var header = SlugGlyph.HeaderLength + (2 * ((vertical ? glyph.HorizontalBandCount : 0) + band));
        var count = (int)glyph.Bands[header];
        var offset = (int)glyph.Bands[header + 1];
        return [.. glyph.Bands.Slice(offset, count).ToArray().Select(v => (int)v)];
    }
}
