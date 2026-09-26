using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

/// <summary>Content inside a rounded clip is cut to its corners.</summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class RendererRoundedClipGpuTests
{
    private const int Size = 64;
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 Red = new(1f, 0f, 0f, 1f);

    [TestMethod]
    public void ContentIsCutToTheRoundedCorners()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(Black, r =>
        {
            r.PushClip(8, 8, 48, 48, 16f);
            r.DrawRectangleFilled(0, 0, Size, Size, Red);
            r.PopClip();
        });

        Assert.AreEqual(0, frame.PixelAt(pixels, 9, 9).R, "the corner is cut away");
        Assert.AreEqual(0, frame.PixelAt(pixels, 54, 54).R, "every corner");
        Assert.AreEqual(255, frame.PixelAt(pixels, 32, 32).R, "the middle is kept");
        Assert.AreEqual(255, frame.PixelAt(pixels, 8, 32).R, "the straight edge is kept to the pixel");
        Assert.AreEqual(0, frame.PixelAt(pixels, 7, 32).R, "and nothing past it");
    }

    [TestMethod]
    public void AtDoubleDensityTheClipIsInDevicePixels()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size, pixelScale: 2f);

        var pixels = frame.Render(Black, r =>
        {
            r.PushClip(8, 8, 48, 48, 16f);
            r.DrawRectangleFilled(0, 0, Size, Size, Red);
            r.PopClip();
        });

        // Logical (8, 8)..(56, 56) is device (16, 16)..(112, 112), with 32-pixel corners.
        Assert.AreEqual(0, frame.PixelAt(pixels, 18, 18).R, "corner cut");
        Assert.AreEqual(255, frame.PixelAt(pixels, 64, 64).R, "middle kept");
        Assert.AreEqual(255, frame.PixelAt(pixels, 16, 64).R, "edge kept");
        Assert.AreEqual(0, frame.PixelAt(pixels, 15, 64).R, "nothing past the edge");
    }

    [TestMethod]
    public void TheCurvedEdgeIsAntiAliased()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(Black, r =>
        {
            r.PushClip(8, 8, 48, 48, 16f);
            r.DrawRectangleFilled(0, 0, Size, Size, Red);
            r.PopClip();
        });

        var partial = 0;
        for (var i = 8; i < 24; i++)
        {
            var red = frame.PixelAt(pixels, i, i).R;
            if (red is > 10 and < 245)
            {
                partial++;
            }
        }
        Assert.IsTrue(partial >= 1, "no partially covered pixel along the corner's diagonal");
    }

    [TestMethod]
    public void EveryKindOfDrawIsClipped()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);

        var pixels = frame.Render(Black, r =>
        {
            r.PushClip(8, 8, 48, 48, 24f); // a circle
            r.DrawRoundedRectFilled(0, 0, Size, Size, 0f, Red);
            r.DrawText(font, "MMM", -20, -10, 90f, Vector4.One);
            r.DrawShadow(0, 0, Size, Size, 0f, 2f, Vector4.One);
            r.PopClip();
        });

        Assert.AreEqual(0, frame.PixelAt(pixels, 10, 10).R + frame.PixelAt(pixels, 10, 10).G, "shapes, text and shadows are all cut");
    }

    [TestMethod]
    public void ARectangleClipInsideARoundedOneKeepsTheRounding()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(Black, r =>
        {
            r.PushClip(8, 8, 48, 48, 16f);
            r.PushClip(0, 0, Size, Size);
            r.DrawRectangleFilled(0, 0, Size, Size, Red);
            r.PopClip();
            r.PopClip();
        });

        Assert.AreEqual(0, frame.PixelAt(pixels, 9, 9).R);
        Assert.AreEqual(255, frame.PixelAt(pixels, 32, 32).R);
    }

    [TestMethod]
    public void DrawsAfterThePopAreNotClipped()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(Black, r =>
        {
            r.PushClip(8, 8, 48, 48, 16f);
            r.PopClip();
            r.DrawRectangleFilled(0, 0, Size, Size, Red);
        });

        Assert.AreEqual(255, frame.PixelAt(pixels, 1, 1).R);
    }

    [TestMethod]
    public void AFrameCanHoldMoreRoundedClipsThanTheUniformBufferStartsWith()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        // 40 different clips, each an 8x8 circle on its own cell.
        var pixels = frame.Render(Black, r =>
        {
            for (var i = 0; i < 40; i++)
            {
                var x = i % 8 * 8;
                var y = i / 8 * 8;
                r.PushClip(x, y, 8, 8, 4f);
                r.DrawRectangleFilled(x, y, 8, 8, Red);
                r.PopClip();
            }
        });

        for (var i = 0; i < 40; i++)
        {
            var x = i % 8 * 8;
            var y = i / 8 * 8;
            Assert.AreEqual(255, frame.PixelAt(pixels, x + 4, y + 4).R, $"cell {i} centre");
            Assert.AreEqual(0, frame.PixelAt(pixels, x, y).R, $"cell {i} corner");
        }
    }
}
