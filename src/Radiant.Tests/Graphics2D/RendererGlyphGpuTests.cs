using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Radiant.Text;
using Color = Radiant.Graphics2D.Color;

namespace Radiant.Tests.Graphics2D;

/// <summary>What text laid out by Radiant.Text looks like once drawn from the coverage atlas.</summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class RendererGlyphGpuTests
{
    private static readonly Vector4 White = Vector4.One;
    private static readonly TextStyle Black16 = new() { Size = 16, Color = new Vector4(0, 0, 0, 1) };

    private static int Ink(GpuFrame frame, byte[] pixels, int x0 = 0, int y0 = 0, int x1 = int.MaxValue, int y1 = int.MaxValue)
    {
        // Darkness summed over a region of a white frame: how much text is there.
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

    [TestMethod]
    public void AParagraphDrawsInsideItsBox()
    {
        using var frame = GpuFrame.CreateOrSkip(128, 48);
        var paragraph = Paragraph.Layout("Hello", Black16);

        var pixels = frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(10, 10)));

        var inside = Ink(frame, pixels, 10, 10, 10 + (int)MathF.Ceiling(paragraph.Width), 10 + (int)MathF.Ceiling(paragraph.Height));
        Assert.IsTrue(inside > 255 * 20, $"ink {inside}");
        Assert.AreEqual(inside, Ink(frame, pixels), "no ink outside the paragraph");
    }

    [TestMethod]
    public void StemsAreSolid()
    {
        using var frame = GpuFrame.CreateOrSkip(64, 64);
        var paragraph = Paragraph.Layout("l", Black16 with { Size = 40 });

        var pixels = frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(20, 5)));

        var black = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            black += pixels[i + 1] == 0 ? 1 : 0;
        }
        Assert.IsTrue(black > 40, $"{black} fully covered pixels");
    }

    [TestMethod]
    public void TextMovedAWholePixelDrawsTheSamePixelsMovedByOne()
    {
        using var frame = GpuFrame.CreateOrSkip(96, 32);
        var paragraph = Paragraph.Layout("Radiant", Black16);

        var at10 = frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(10, 8)));
        var at11 = frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(11, 8)));

        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x + 1 < frame.Width; x++)
            {
                Assert.AreEqual(frame.PixelAt(at10, x, y), frame.PixelAt(at11, x + 1, y), $"({x}, {y})");
            }
        }
    }

    [TestMethod]
    public void AQuarterPixelMoveIsDrawnAsAQuarterPixelMove()
    {
        using var frame = GpuFrame.CreateOrSkip(64, 32);
        var paragraph = Paragraph.Layout("l", Black16 with { Size = 24 });

        var at0 = frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(20, 4)));
        var atQuarter = frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(20.25f, 4)));

        Assert.AreEqual(Centroid(frame, at0) + 0.25f, Centroid(frame, atQuarter), 0.05f);
    }

    [TestMethod]
    public void BaselinesLandOnWholePixels()
    {
        using var frame = GpuFrame.CreateOrSkip(64, 48);
        var paragraph = Paragraph.Layout("H", Black16 with { Size = 20 });
        // Placed so the baseline falls 0.7 of the way through a pixel row; it snaps to the next row.
        var top = 29.7f - paragraph.FirstBaseline;

        var pixels = frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(20, top)));

        var darkestAbove = 0;
        for (var x = 0; x < frame.Width; x++)
        {
            darkestAbove = Math.Max(darkestAbove, 255 - frame.PixelAt(pixels, x, 29).G);
        }
        Assert.AreEqual(255, darkestAbove, "the stems end on the pixel boundary at y = 30, so their last row is solid");
        Assert.AreEqual(0, Ink(frame, pixels, 0, 30, frame.Width, 31), "nothing below the baseline");
    }

    [TestMethod]
    public void RetinaTextIsRasterizedAtItsDevicePixelSize()
    {
        using var one = GpuFrame.CreateOrSkip(96, 32);
        using var two = GpuFrame.CreateOrSkip(96, 32, pixelScale: 2f);
        var paragraph = Paragraph.Layout("Radiant", Black16);

        var inkAt1 = Ink(one, one.Render(White, r => r.DrawParagraph(paragraph, new Vector2(4, 4))));
        var inkAt2 = Ink(two, two.Render(White, r => r.DrawParagraph(paragraph, new Vector2(4, 4))));

        // Four times the pixels; within a few percent, since the gamma correction depends on the
        // share of edge pixels, which is smaller at 2x.
        Assert.AreEqual(4f, inkAt2 / (float)inkAt1, 0.3f);
    }

    [TestMethod]
    public void GammaCorrectionGivesDarkTextOnLightItsWeight()
    {
        using var frame = GpuFrame.CreateOrSkip(96, 32);
        var paragraph = Paragraph.Layout("Radiant", Black16);

        frame.Renderer.TextGamma = 1f;
        var linear = Ink(frame, frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(4, 4))));
        frame.Renderer.TextGamma = 1.8f;
        var corrected = Ink(frame, frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(4, 4))));

        Assert.IsTrue(corrected > linear * 1.05f, $"{corrected} vs {linear}");
    }

    [TestMethod]
    public void TextTakesItsStyleColourOrTheOverride()
    {
        using var frame = GpuFrame.CreateOrSkip(64, 64);
        var red = Paragraph.Layout("l", Black16 with { Size = 40, Color = Color.Parse("#ff0000") });

        var styled = frame.Render(Vector4.Zero, r => r.DrawParagraph(red, new Vector2(20, 5)));
        var overridden = frame.Render(Vector4.Zero, r => r.DrawParagraph(red, new Vector2(20, 5), Color.Parse("#0000ff")));

        var (sx, sy) = Darkest(frame, styled, alpha: true);
        Assert.AreEqual((255, 0, 0), (frame.PixelAt(styled, sx, sy).R, frame.PixelAt(styled, sx, sy).G, frame.PixelAt(styled, sx, sy).B));
        Assert.AreEqual(255, frame.PixelAt(overridden, sx, sy).B);
        Assert.AreEqual(0, frame.PixelAt(overridden, sx, sy).R);
    }

    [TestMethod]
    public void TextDrawsInOrderWithShapes()
    {
        using var frame = GpuFrame.CreateOrSkip(96, 32);
        var paragraph = Paragraph.Layout("Radiant", Black16);

        var pixels = frame.Render(White, r =>
        {
            r.DrawParagraph(paragraph, new Vector2(4, 4));
            r.DrawRectangleFilled(0, 0, 96, 32, White);
        });

        Assert.AreEqual(0, Ink(frame, pixels), "the rectangle drawn after covers the text");
    }

    [TestMethod]
    public void ATranslationMovesTextExactly()
    {
        using var frame = GpuFrame.CreateOrSkip(96, 32);
        var paragraph = Paragraph.Layout("Radiant", Black16);

        var direct = frame.Render(White, r => r.DrawParagraph(paragraph, new Vector2(9, 6)));
        var translated = frame.Render(White, r =>
        {
            r.PushTransform(Matrix3x2.CreateTranslation(5, 2));
            r.DrawParagraph(paragraph, new Vector2(4, 4));
            r.PopTransform();
        });

        CollectionAssert.AreEqual(direct, translated);
    }

    [TestMethod]
    public void ClipsCutText()
    {
        using var frame = GpuFrame.CreateOrSkip(96, 32);
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
    public void AFullAtlasIsEmptiedBetweenFramesAndTextStillDraws()
    {
        using var frame = GpuFrame.CreateOrSkip(128, 128);
        var atlas = frame.Renderer.GlyphAtlas!;
        const string text = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var size = 60f;
        while (atlas.PageCount <= GlyphAtlas.MaxPages)
        {
            var paragraph = Paragraph.Layout(text, Black16 with { Size = size++ });
            frame.Render(White, r => r.DrawParagraph(paragraph, Vector2.Zero));
        }
        var small = Paragraph.Layout("Radiant", Black16);

        var pixels = frame.Render(White, r => r.DrawParagraph(small, new Vector2(4, 4)));

        Assert.AreEqual(1, atlas.PageCount);
        Assert.IsTrue(Ink(frame, pixels) > 255 * 20);
    }

    private static float Centroid(GpuFrame frame, byte[] pixels)
    {
        float sum = 0, weighted = 0;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                // Coverage from sRGB-encoded darkness is not linear, but the same bias applies to both frames.
                var ink = 255 - frame.PixelAt(pixels, x, y).G;
                sum += ink;
                weighted += ink * (x + 0.5f);
            }
        }
        return weighted / sum;
    }

    private static (int X, int Y) Darkest(GpuFrame frame, byte[] pixels, bool alpha)
    {
        var best = (0, 0);
        var bestValue = -1;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var value = alpha ? frame.PixelAt(pixels, x, y).A : 255 - frame.PixelAt(pixels, x, y).G;
                if (value > bestValue)
                {
                    bestValue = value;
                    best = (x, y);
                }
            }
        }
        return best;
    }
}
