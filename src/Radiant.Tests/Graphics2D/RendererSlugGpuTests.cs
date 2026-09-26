using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Radiant.Text;
using Radiant.Text.Slug;
using Color = Radiant.Graphics2D.Color;

namespace Radiant.Tests.Graphics2D;

/// <summary>What text drawn with Slug, from its outlines on the GPU, looks like.</summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class RendererSlugGpuTests
{
    private static readonly Vector4 White = Vector4.One;
    private static readonly TextStyle Black16 = new() { Size = 16, Color = new Vector4(0, 0, 0, 1) };

    private static GpuFrame SlugFrame(int width, int height, float pixelScale = 1f)
    {
        var frame = GpuFrame.CreateOrSkip(width, height, pixelScale);
        frame.Renderer.TextRendering = TextRendering.Slug;
        return frame;
    }

    // Darkness summed over a region of a white frame: how much text is there.
    private static int Ink(GpuFrame frame, byte[] pixels, int x0 = 0, int y0 = 0, int x1 = int.MaxValue, int y1 = int.MaxValue)
    {
        var ink = 0;
        for (var y = Math.Max(0, y0); y < Math.Min(frame.Height, y1); y++)
        {
            for (var x = Math.Max(0, x0); x < Math.Min(frame.Width, x1); x++)
            {
                ink += 255 - frame.PixelAt(pixels, x, y).G;
            }
        }
        return ink;
    }

    private static int Darkness(GpuFrame frame, byte[] pixels, int x, int y) => 255 - frame.PixelAt(pixels, x, y).G;

    // Where to put a paragraph so its first baseline falls on a whole pixel, as coverage text snaps it.
    private static Vector2 OnPixelGrid(Paragraph paragraph, float x, float y) =>
        new(x, MathF.Round(y + paragraph.FirstBaseline) - paragraph.FirstBaseline);

    [TestMethod]
    public void SlugTextDrawsInsideItsParagraphBox()
    {
        using var frame = SlugFrame(128, 48);
        var paragraph = Paragraph.Layout("Hello", Black16);

        var pixels = frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(10, 10)));

        var inside = Ink(frame, pixels, 10, 10, 10 + (int)MathF.Ceiling(paragraph.Width), 10 + (int)MathF.Ceiling(paragraph.Height));
        Assert.IsTrue(inside > 255 * 20, $"ink {inside}");
        Assert.AreEqual(inside, Ink(frame, pixels), "no ink outside the paragraph");
        Assert.AreEqual(6 * 5, frame.Renderer.SlugVertices.Count, "one quad per glyph");
    }

    [TestMethod]
    public void AtAMediumSizeSlugInkMatchesCoverageInk()
    {
        using var frame = SlugFrame(320, 40);
        var paragraph = Paragraph.Layout("Sphinx of black quartz, judge my vow 0123", Black16 with { Size = 18 });
        var at = OnPixelGrid(paragraph, 4, 8);

        var slug = frame.Render(White, r => r.DrawParagraph(paragraph, at));
        frame.Renderer.TextRendering = TextRendering.Coverage;
        var coverage = frame.Render(White, r => r.DrawParagraph(paragraph, at));

        // Coverage text puts each pen on a quarter pixel; Slug draws it where it is. So compare
        // over the inked area, which both draw the same text into.
        long difference = 0;
        var inked = 0;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var a = Darkness(frame, slug, x, y);
                var b = Darkness(frame, coverage, x, y);
                if (a > 0 || b > 0)
                {
                    difference += Math.Abs(a - b);
                    inked++;
                }
            }
        }
        var mean = difference / (float)inked;
        var ratio = Ink(frame, slug) / (float)Ink(frame, coverage);
        Assert.IsTrue(mean < 16f, $"mean difference {mean:F1} / 255 over {inked} inked pixels");
        Assert.AreEqual(1f, ratio, 0.03f, $"ink ratio {ratio:F3}");
    }

    [TestMethod]
    public void UnderEightTimesMagnificationEdgesStayOnePixelWide()
    {
        using var frame = SlugFrame(200, 160);
        var paragraph = Paragraph.Layout("H", Black16);

        var pixels = frame.Render(White, r =>
        {
            r.PushTransform(Matrix3x2.CreateScale(8f) * Matrix3x2.CreateTranslation(20.3f, -10.37f));
            r.DrawParagraph(paragraph, Vector2.Zero);
            r.PopTransform();
        });

        // A row through the stems (vertical edges, found by the ray cast right) and a column
        // through the crossbar (horizontal edges, found by the ray cast up): each edge falls
        // part-way through a pixel, and is that one partly covered pixel: anti-aliased, but not a
        // blur several pixels wide, as a bitmap scaled up would be.
        var (row, rowEdges, rowPartial) = Profile(frame, pixels, horizontal: true, at: frame.Height / 3);
        var (column, columnEdges, columnPartial) = FindCrossbarColumn(frame, pixels);
        Assert.AreEqual(4, rowEdges, $"two stems in row {row}");
        Assert.AreEqual(rowEdges, rowPartial, $"partly covered pixels for {rowEdges} edges in row {row}");
        Assert.AreEqual(2, columnEdges, $"the crossbar in column {column}");
        Assert.AreEqual(columnEdges, columnPartial, $"partly covered pixels for {columnEdges} edges in column {column}");
    }

    [TestMethod]
    public void RotatedTextKeepsSolidStems()
    {
        using var frame = SlugFrame(64, 64);
        var paragraph = Paragraph.Layout("l", Black16 with { Size = 40 });

        var upright = frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(20, 5)));
        var rotated = frame.Render(White, r =>
        {
            r.PushTransform(Matrix3x2.CreateRotation(MathF.PI / 6f, new Vector2(32, 32)));
            r.DrawParagraph(paragraph, new Vector2(20, 5));
            r.PopTransform();
        });

        var solidUpright = Solid(frame, upright);
        var solidRotated = Solid(frame, rotated);
        Assert.IsTrue(solidUpright > 40, $"{solidUpright} fully covered pixels upright");
        Assert.IsTrue(solidRotated > solidUpright * 0.7f, $"{solidRotated} fully covered pixels rotated, {solidUpright} upright");
        Assert.AreEqual(1f, Ink(frame, rotated) / (float)Ink(frame, upright), 0.03f, "a rotation neither adds ink nor loses it");
    }

    [TestMethod]
    public void AtAnyAngleTheGlyphsCoverTheirArea()
    {
        // The quad is grown by a pixel square's reach across each edge, which at 45° is more than
        // half a pixel: with too little, the pixels just outside a glyph's box lose their ink. A
        // heavy I is a rectangle filling its box, so every edge pixel is at stake.
        // Alpha over a transparent frame, with no gamma correction, sums each pixel's coverage, so
        // the total is the glyph's area; exact-area coverage text gives the reference.
        using var frame = SlugFrame(96, 96);
        frame.Renderer.TextGamma = 1f;
        var paragraph = Paragraph.Layout("I", Black16 with { Size = 48, Weight = FontWeight.Black });
        var at = OnPixelGrid(paragraph, 40, 20);
        frame.Renderer.TextRendering = TextRendering.Coverage;
        var area = Alpha(frame, frame.Render(Vector4.Zero, r => r.DrawParagraph(paragraph, at)));
        frame.Renderer.TextRendering = TextRendering.Slug;

        var worst = 0f;
        var ratios = "";
        for (var degrees = 0; degrees < 360; degrees += 15)
        {
            var angle = degrees * MathF.PI / 180f;
            var covered = Alpha(frame, frame.Render(Vector4.Zero, r =>
            {
                r.PushTransform(Matrix3x2.CreateRotation(angle, new Vector2(48, 48)));
                r.DrawParagraph(paragraph, at);
                r.PopTransform();
            }));
            worst = MathF.Max(worst, MathF.Abs((covered / area) - 1f));
            ratios += $"{degrees}°: {covered / area:F4}  ";
        }
        Assert.IsTrue(worst < 0.004f, ratios);
    }

    [TestMethod]
    public void TheGpuComputesWhatTheReferenceEvaluatorComputes()
    {
        // Every pixel of a turned and scaled glyph, against SlugCoverage run on the CPU at the
        // pixel's centre: the shader's coverage, its band lookup, and the quad reaching every pixel
        // with any coverage at all.
        using var frame = SlugFrame(128, 128);
        frame.Renderer.TextGamma = 1f;
        var style = Black16 with { Size = 40, Weight = FontWeight.Bold };
        var paragraph = Paragraph.Layout("R", style);
        var run = paragraph.Lines[0].Runs[0];
        var shaped = run.Shaped;
        var glyph = SlugGlyphBuilder.Build(shaped.Font.GetOutline(shaped.Glyphs[0]), shaped.Font.Face.UnitsPerEm);
        var at = new Vector2(44, 30);
        var transform = Matrix3x2.CreateScale(1.3f, 0.9f) * Matrix3x2.CreateRotation(MathF.PI / 4f, new Vector2(64, 64));

        var pixels = frame.Render(Vector4.Zero, r =>
        {
            r.PushTransform(transform);
            r.DrawParagraph(paragraph, at);
            r.PopTransform();
        });

        Assert.IsTrue(Matrix3x2.Invert(transform, out var inverse));
        var origin = run.Origin + at + shaped.Offsets[0];
        // Pixels per em as the shader's fwidth finds them: from how em changes across a pixel.
        var pixelsPerEm = new Vector2(
            shaped.Size / (MathF.Abs(inverse.M11) + MathF.Abs(inverse.M21)),
            shaped.Size / (MathF.Abs(inverse.M12) + MathF.Abs(inverse.M22)));
        var worst = 0;
        var inked = 0;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var local = Vector2.Transform(new Vector2(x + 0.5f, y + 0.5f), inverse) - origin;
                var expected = SlugCoverage.Evaluate(glyph, new Vector2(local.X, -local.Y) / shaped.Size, pixelsPerEm) * 255f;
                var actual = frame.PixelAt(pixels, x, y).A;
                worst = Math.Max(worst, (int)MathF.Abs(actual - expected));
                inked += actual > 0 ? 1 : 0;
            }
        }
        Assert.IsTrue(inked > 400, $"{inked} pixels inked");
        Assert.IsTrue(worst <= 1, $"a pixel differs by {worst} / 255");
    }

    [TestMethod]
    public void PixelsJustOutsideTheGlyphBoxAreShaded()
    {
        using var frame = SlugFrame(64, 64);
        var style = Black16 with { Size = 40 };
        var paragraph = Paragraph.Layout("I", style);
        var font = paragraph.Lines[0].Runs[0].Shaped.Font;
        var glyph = paragraph.Lines[0].Runs[0].Shaped.Glyphs[0];
        var left = font.GetOutline(glyph).Bounds.Min.X * font.Scale(style.Size);
        // The stem's left edge 0.3 px into pixel column 20, so that column's centre is outside the
        // glyph's box: only a grown quad reaches it.
        var x = 20.7f - left - paragraph.Lines[0].Runs[0].Origin.X;

        var pixels = frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(x, 5)));

        var middle = frame.Height / 2;
        Assert.AreEqual(0, Darkness(frame, pixels, 19, middle), "column 19 is clear of the stem");
        var edge = Darkness(frame, pixels, 20, middle);
        Assert.IsTrue(edge is > 40 and < 215, $"column 20 is partly covered: {edge}");
        Assert.AreEqual(255, Darkness(frame, pixels, 21, middle), "column 21 is inside the stem");
    }

    [TestMethod]
    public void OverlappingContoursOfAHeavyWeightShowNoSeamsOrHoles()
    {
        using var frame = SlugFrame(360, 120);
        // Heavy Inter's contours overlap in these glyphs (SlugCoverageTests finds them independently).
        var paragraph = Paragraph.Layout("#AKM$&", Black16 with { Size = 80, Weight = FontWeight.Black });
        var at = OnPixelGrid(paragraph, 6, 4);

        var slug = frame.Render(White, r => r.DrawParagraph(paragraph, at));
        frame.Renderer.TextRendering = TextRendering.Coverage;
        var coverage = frame.Render(White, r => r.DrawParagraph(paragraph, at));

        // Wherever exact coverage is solid a pixel in from every side, Slug must be solid too.
        var interior = 0;
        var seams = 0;
        for (var y = 1; y + 1 < frame.Height; y++)
        {
            for (var x = 1; x + 1 < frame.Width; x++)
            {
                if (!SolidAround(frame, coverage, x, y))
                {
                    continue;
                }
                interior++;
                seams += Darkness(frame, slug, x, y) < 255 ? 1 : 0;
            }
        }
        Assert.IsTrue(interior > 2000, $"{interior} interior pixels");
        Assert.AreEqual(0, seams, $"{seams} of {interior} interior pixels are not solid");
    }

    [TestMethod]
    public void TheTintIsAStraightAlphaColourPremultipliedOnTheGpu()
    {
        using var frame = SlugFrame(64, 64);
        var paragraph = Paragraph.Layout("l", Black16 with { Size = 40 });

        var red = frame.Render(Vector4.Zero, r => r.DrawParagraph(paragraph, new Vector2(20, 5), Color.Parse("#ff000080")));

        var (x, y) = MostOpaque(frame, red);
        var (r, g, b, a) = frame.PixelAt(red, x, y);
        // Half-transparent red over nothing: alpha 128, and red premultiplied to half its light,
        // which sRGB encodes as 188.
        Assert.AreEqual(128, a, 1);
        Assert.AreEqual(188, r, 2);
        Assert.AreEqual((0, 0), (g, b));
    }

    [TestMethod]
    public void RunsTakeTheirStyleColourOrTheOverride()
    {
        using var frame = SlugFrame(64, 64);
        var red = Paragraph.Layout("l", Black16 with { Size = 40, Color = Color.Parse("#ff0000") });

        var styled = frame.Render(Vector4.Zero, r => r.DrawParagraph(red, new Vector2(20, 5)));
        var overridden = frame.Render(Vector4.Zero, r => r.DrawParagraph(red, new Vector2(20, 5), Color.Parse("#0000ff")));

        var (x, y) = MostOpaque(frame, styled);
        Assert.AreEqual((255, 0, 0, 255), frame.PixelAt(styled, x, y));
        Assert.AreEqual((0, 0, 255, 255), frame.PixelAt(overridden, x, y));
    }

    [TestMethod]
    public void ClipsCutSlugText()
    {
        using var frame = SlugFrame(96, 32);
        var paragraph = Paragraph.Layout("Radiant", Black16);

        var pixels = frame.Render(White, r =>
        {
            r.PushClip(0, 0, 30, 32);
            r.DrawParagraph(paragraph, new Vector2(4, 4));
            r.PopClip();
        });

        Assert.IsTrue(Ink(frame, pixels, 0, 0, 30, 32) > 0);
        Assert.AreEqual(0, Ink(frame, pixels, 30, 0));
    }

    [TestMethod]
    public void RoundedClipsCutSlugTextAtTheirCorners()
    {
        using var frame = SlugFrame(96, 48);
        var paragraph = Paragraph.Layout("Radiant", Black16 with { Size = 24 });
        // The R's cap top a couple of pixels below the frame's top.
        var at = new Vector2(1, 3 - (paragraph.FirstBaseline - paragraph.Lines[0].Runs[0].Shaped.Font.Metrics(24).CapHeight));

        var plain = frame.Render(White, r => r.DrawParagraph(paragraph, at));
        var rounded = frame.Render(White, r =>
        {
            r.PushClip(0, 0, 96, 48, 20f);
            r.DrawParagraph(paragraph, at);
            r.PopClip();
        });

        // The R's top-left corner is outside a 20 px corner; the rest of the word is not.
        Assert.IsTrue(Ink(frame, plain, 0, 0, 8, 8) > 255 * 4, $"{Ink(frame, plain, 0, 0, 8, 8)}");
        Assert.AreEqual(0, Ink(frame, rounded, 0, 0, 6, 6));
        Assert.AreEqual(Ink(frame, plain, 30, 0), Ink(frame, rounded, 30, 0));
    }

    [TestMethod]
    public void SlugTextDrawsInOrderWithShapes()
    {
        using var frame = SlugFrame(96, 32);
        var paragraph = Paragraph.Layout("Radiant", Black16);

        var covered = frame.Render(White, r =>
        {
            r.DrawParagraph(paragraph, new Vector2(4, 4));
            r.DrawRectangleFilled(0, 0, 96, 32, White);
        });
        var onTop = frame.Render(White, r =>
        {
            r.DrawRectangleFilled(0, 0, 96, 32, Color.Parse("#ff0000"));
            r.DrawParagraph(paragraph, new Vector2(4, 4));
        });

        Assert.AreEqual(0, Ink(frame, covered), "the rectangle drawn after covers the text");
        var (x, y) = Darkest(frame, onTop);
        Assert.AreEqual((0, 0, 0), (frame.PixelAt(onTop, x, y).R, frame.PixelAt(onTop, x, y).G, frame.PixelAt(onTop, x, y).B),
            "text drawn after the rectangle is on top of it");
    }

    [TestMethod]
    public void SlugAndCoverageTextInterleaveInDrawOrder()
    {
        using var frame = SlugFrame(96, 32);
        var paragraph = Paragraph.Layout("Radiant", Black16);

        var pixels = frame.Render(White, r =>
        {
            r.TextRendering = TextRendering.Coverage;
            r.DrawParagraph(paragraph, new Vector2(4, 4));
            r.DrawRectangleFilled(0, 0, 96, 32, White);
            r.TextRendering = TextRendering.Slug;
            r.DrawParagraph(paragraph, new Vector2(4, 4));
        });

        Assert.IsTrue(Ink(frame, pixels) > 255 * 20, "the Slug text drawn last shows");
    }

    [TestMethod]
    public void ATranslationMovesSlugTextExactly()
    {
        using var frame = SlugFrame(96, 32);
        var paragraph = Paragraph.Layout("Radiant", Black16);

        var direct = frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(9, 6)));
        var translated = frame.Render(White, r =>
        {
            r.PushTransform(Matrix3x2.CreateTranslation(5, 2));
            r.DrawParagraph(paragraph, new Vector2(4, 4));
            r.PopTransform();
        });

        for (var i = 0; i < direct.Length; i++)
        {
            Assert.AreEqual(direct[i], translated[i], 1, $"byte {i}");
        }
    }

    [TestMethod]
    public void RetinaSlugTextHasFourTimesTheInk()
    {
        using var one = SlugFrame(96, 32);
        using var two = SlugFrame(96, 32, pixelScale: 2f);
        var paragraph = Paragraph.Layout("Radiant", Black16);

        var inkAt1 = Ink(one, one.Render(White, r => r.DrawParagraph(paragraph, new Vector2(4, 4))));
        var inkAt2 = Ink(two, two.Render(White, r => r.DrawParagraph(paragraph, new Vector2(4, 4))));

        Assert.AreEqual(4f, inkAt2 / (float)inkAt1, 0.3f);
    }

    [TestMethod]
    public void GlyphsArePreparedOnceWhateverTheSizeOrTransform()
    {
        using var frame = SlugFrame(256, 128);
        var small = Paragraph.Layout("Radiant", Black16 with { Size = 40 });
        var large = Paragraph.Layout("Radiant", Black16 with { Size = 80 });
        Assert.AreSame(small.Lines[0].Runs[0].Shaped.Font, large.Lines[0].Runs[0].Shaped.Font, "one optical size above 32 px");

        frame.Render(White, r => r.DrawParagraph(small, Vector2.Zero));
        var prepared = frame.Renderer.SlugGlyphs!.GlyphCount;
        frame.Render(White, r =>
        {
            r.DrawParagraph(large, Vector2.Zero);
            r.PushTransform(Matrix3x2.CreateRotation(1f) * Matrix3x2.CreateScale(3f));
            r.DrawParagraph(small, Vector2.Zero);
            r.PopTransform();
        });

        Assert.AreEqual(6, prepared, "R a d i n t");
        Assert.AreEqual(prepared, frame.Renderer.SlugGlyphs.GlyphCount);
    }

    // Alpha summed over a frame drawn on transparent: coverage, in 255ths of a pixel.
    private static float Alpha(GpuFrame frame, byte[] pixels)
    {
        var sum = 0f;
        for (var i = 3; i < pixels.Length; i += 4)
        {
            sum += pixels[i];
        }
        return sum;
    }

    // Pixels of a white frame that are fully black.
    private static int Solid(GpuFrame frame, byte[] pixels)
    {
        var solid = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            solid += pixels[i + 1] == 0 ? 1 : 0;
        }
        return solid;
    }

    private static bool SolidAround(GpuFrame frame, byte[] pixels, int x, int y)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (frame.PixelAt(pixels, x + dx, y + dy).G != 0)
                {
                    return false;
                }
            }
        }
        return true;
    }

    // Along a row (or column): how many edges there are (changes between clear and solid), and how
    // many pixels are neither clear nor solid.
    private static (int At, int Edges, int Partial) Profile(GpuFrame frame, byte[] pixels, bool horizontal, int at)
    {
        var length = horizontal ? frame.Width : frame.Height;
        var edges = 0;
        var partial = 0;
        var wasInk = false;
        for (var i = 0; i < length; i++)
        {
            var darkness = horizontal ? Darkness(frame, pixels, i, at) : Darkness(frame, pixels, at, i);
            if (darkness is > 2 and < 253)
            {
                partial++;
            }
            var ink = darkness >= 128;
            if (ink != wasInk)
            {
                edges++;
                wasInk = ink;
            }
        }
        return (at, edges, partial);
    }

    // A column through the H's crossbar only: the one between the stems with the fewest inked pixels.
    private static (int At, int Edges, int Partial) FindCrossbarColumn(GpuFrame frame, byte[] pixels)
    {
        var row = frame.Height / 3;
        var inStem = false;
        var stemsEnded = 0;
        for (var x = 0; x < frame.Width; x++)
        {
            var ink = Darkness(frame, pixels, x, row) >= 128;
            if (inStem && !ink && ++stemsEnded == 1)
            {
                // Just past the first stem: go a few pixels further, clear of its edge.
                return Profile(frame, pixels, horizontal: false, at: x + 8);
            }
            inStem = ink;
        }
        return (-1, 0, 0);
    }

    private static (int X, int Y) MostOpaque(GpuFrame frame, byte[] pixels)
    {
        var best = (0, 0);
        var bestValue = -1;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                if (frame.PixelAt(pixels, x, y).A > bestValue)
                {
                    bestValue = frame.PixelAt(pixels, x, y).A;
                    best = (x, y);
                }
            }
        }
        return best;
    }

    private static (int X, int Y) Darkest(GpuFrame frame, byte[] pixels)
    {
        var best = (0, 0);
        var bestValue = int.MaxValue;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var (r, g, b, _) = frame.PixelAt(pixels, x, y);
                if (r + g + b < bestValue)
                {
                    bestValue = r + g + b;
                    best = (x, y);
                }
            }
        }
        return best;
    }
}
