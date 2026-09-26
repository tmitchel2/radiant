using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Silk.NET.WebGPU;
using Color = Radiant.Graphics2D.Color;

namespace Radiant.Tests.Graphics2D;

/// <summary>
/// A later draw covers an earlier one whatever pipelines the two use. Draw order used to follow
/// primitive type (filled, lines, SDF shapes, images, then text), so text showed through anything
/// drawn over it.
/// </summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class RendererDrawOrderGpuTests
{
    private const int Size = 64;

    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    private static readonly Color Red = Color.Parse("#ff0000");
    private static readonly Color Green = Color.Parse("#00ff00");
    private static readonly Color Blue = Color.Parse("#0000ff");
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    [TestMethod]
    public void ARectDrawnOverTextHidesTheText()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);

        var pixels = frame.Render(Black, r =>
        {
            r.DrawText(font, "III", 4, 0, 56f, White);
            r.DrawRectangleFilled(0, 0, Size, Size, Red);
        });

        AssertEverywhere(frame, pixels, 0xff, 0, 0, "text must not show through the rect drawn after it");
    }

    [TestMethod]
    public void ARoundedRectDrawnOverTextHidesTheText()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);

        var pixels = frame.Render(Black, r =>
        {
            r.DrawText(font, "III", 4, 0, 56f, White);
            r.DrawRoundedRectFilled(-10, -10, Size + 20, Size + 20, 4f, Green);
        });

        AssertEverywhere(frame, pixels, 0, 0xff, 0, "text must not show through the shape drawn after it");
    }

    [TestMethod]
    public void AFilledRectDrawnOverARoundedRectHidesIt()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(Black, r =>
        {
            r.DrawRoundedRectFilled(0, 0, Size, Size, 8f, Green);
            r.DrawRectangleFilled(0, 0, Size, Size, Red);
        });

        AssertEverywhere(frame, pixels, 0xff, 0, 0, "the SDF pipeline used to draw after the filled one");
    }

    [TestMethod]
    public void AnImageDrawnOverTextHidesTheText()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);
        using var texture = SolidTexture(frame, 0xFF, 0, 0xFF);

        var pixels = frame.Render(Black, r =>
        {
            r.DrawText(font, "III", 4, 0, 56f, White);
            r.DrawImage(texture, 0, 0, Size, Size);
        });

        AssertEverywhere(frame, pixels, 0xff, 0, 0xff, "text must not show through the image drawn after it");
    }

    [TestMethod]
    public void InterleavedKindsStackInTheOrderTheyWereDrawn()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(Black, r =>
        {
            r.DrawRectangleFilled(0, 0, 64, 64, Red);        // filled
            r.DrawRoundedRectFilled(8, 8, 48, 48, 0f, Green); // SDF over it
            r.DrawRectangleFilled(16, 16, 32, 32, Blue);     // filled again, over the SDF
        });

        Assert.AreEqual((0xff, 0, 0), Rgb(frame.PixelAt(pixels, 4, 4)));
        Assert.AreEqual((0, 0xff, 0), Rgb(frame.PixelAt(pixels, 12, 12)));
        Assert.AreEqual((0, 0, 0xff), Rgb(frame.PixelAt(pixels, 32, 32)));
    }

    [TestMethod]
    public void AFrameWithoutClippingStillDrawsShapesAndText()
    {
        // BeginFrame() without an attachment size used to draw only filled and line geometry.
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);

        var pixels = frame.Render(Black, r =>
        {
            r.DrawRoundedRectFilled(0, 0, 32, Size, 4f, Green);
            r.DrawText(font, "I", 40, 0, 56f, White);
        }, clipping: false);

        Assert.AreEqual((0, 0xff, 0), Rgb(frame.PixelAt(pixels, 16, 32)), "SDF shape");
        var textCoverage = 0;
        for (var x = 32; x < Size; x++)
        {
            textCoverage += frame.PixelAt(pixels, x, 32).R;
        }
        Assert.IsTrue(textCoverage > 255, "text");
    }

    [TestMethod]
    public void ImagesScrollWithTheRestOfTheContent()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        using var texture = SolidTexture(frame, 0, 0xFF, 0);

        var pixels = frame.Render(Black, r =>
        {
            r.PushScrollOffset(new Vector2(0, 32));
            r.DrawImage(texture, 0, 0, Size, 16);
            r.PopScrollOffset();
        });

        Assert.AreEqual((0, 0, 0), Rgb(frame.PixelAt(pixels, 32, 8)), "the image should have moved down");
        Assert.AreEqual((0, 0xff, 0), Rgb(frame.PixelAt(pixels, 32, 40)), "to 32..48");
    }

    private static Texture2D SolidTexture(GpuFrame frame, byte r, byte g, byte b)
    {
        var texture = Texture2D.Create(frame.Renderer, 2, 2, TextureFormat.Bgra8UnormSrgb);
        var pixels = new byte[16];
        for (var i = 0; i < 4; i++)
        {
            pixels[i * 4] = b;
            pixels[i * 4 + 1] = g;
            pixels[i * 4 + 2] = r;
            pixels[i * 4 + 3] = 0xFF;
        }
        texture.Update(pixels);
        return texture;
    }

    private static (int, int, int) Rgb((byte R, byte G, byte B, byte A) pixel) => (pixel.R, pixel.G, pixel.B);

    private static void AssertEverywhere(GpuFrame frame, byte[] pixels, int r, int g, int b, string message)
    {
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var (pr, pg, pb, _) = frame.PixelAt(pixels, x, y);
                if (Math.Abs(pr - r) > 1 || Math.Abs(pg - g) > 1 || Math.Abs(pb - b) > 1)
                {
                    Assert.Fail($"({x},{y}) is ({pr},{pg},{pb}); {message}");
                }
            }
        }
    }
}
