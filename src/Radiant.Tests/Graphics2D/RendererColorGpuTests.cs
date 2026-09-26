using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Silk.NET.WebGPU;
using Color = Radiant.Graphics2D.Color;

namespace Radiant.Tests.Graphics2D;

/// <summary>
/// What a frame actually contains once the GPU has drawn it: sRGB encoding, linear-light blending,
/// premultiplied alpha, and a composited frame being identical to the frame it came from.
/// </summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class RendererColorGpuTests
{
    private const int Size = 64;

    private static readonly Vector4 OpaqueBlack = new(0f, 0f, 0f, 1f);

    [TestMethod]
    public void AnOpaqueFillReadsBackAsTheHexItWasGiven()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var blue = Color.Parse("#3b82f6");

        var pixels = frame.Render(OpaqueBlack, r =>
        {
            r.DrawRectangleFilled(0, 0, Size / 2, Size, blue);
            r.DrawRoundedRectFilled(Size / 2, 0, Size / 2, Size, 4f, blue);
        });

        Assert.AreEqual((0x3b, 0x82, 0xf6, 255), ToInts(frame.PixelAt(pixels, Size / 4, Size / 2)), "filled pipeline");
        Assert.AreEqual((0x3b, 0x82, 0xf6, 255), ToInts(frame.PixelAt(pixels, 3 * Size / 4, Size / 2)), "SDF pipeline");
    }

    [TestMethod]
    public void HalfTransparentWhiteOverBlackIsBlendedInLinearLight()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(OpaqueBlack, r => r.DrawRectangleFilled(0, 0, Size, Size, new Vector4(1f, 1f, 1f, 0.5f)));

        // Half the light is sRGB 188. Blending encoded values instead would give 128.
        AssertChannel(188, frame.PixelAt(pixels, Size / 2, Size / 2).R);
    }

    [TestMethod]
    public void OverlappingTranslucentFillsAccumulateCoverage()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var halfRed = new Vector4(1f, 0f, 0f, 0.5f);

        var pixels = frame.Render(Vector4.Zero, r =>
        {
            r.DrawRectangleFilled(0, 0, 40, Size, halfRed);
            r.DrawRectangleFilled(24, 0, 40, Size, halfRed);
        });

        var single = frame.PixelAt(pixels, 8, Size / 2);
        var both = frame.PixelAt(pixels, 32, Size / 2);
        AssertChannel(128, single.A);
        AssertChannel(SrgbTransfer.ToSrgbByte(0.5f), single.R);
        // Two 50% layers cover 75%. Straight-alpha blending with alpha written One/Zero kept 50%.
        AssertChannel(191, both.A);
        AssertChannel(SrgbTransfer.ToSrgbByte(0.75f), both.R);
    }

    [TestMethod]
    public void ATranslucentClearIsStoredPremultiplied()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(new Vector4(1f, 0f, 0f, 0.5f), _ => { });

        var pixel = frame.PixelAt(pixels, 0, 0);
        AssertChannel(SrgbTransfer.ToSrgbByte(0.5f), pixel.R);
        AssertChannel(128, pixel.A);
    }

    [TestMethod]
    public void ABorderOnlyStrokeBlendsCleanlyIntoItsBackground()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var grey = Color.Parse("#808080");
        // A transparent fill whose RGB is red. Only its alpha should matter; mixing the border over
        // it in straight alpha dragged the stroke's inner edge towards red.
        var transparentRed = new Vector4(1f, 0f, 0f, 0f);

        var pixels = frame.Render(grey, r => r.DrawRoundedRect(8, 8, 48, 48, 12f, 3f, transparentRed, grey));

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var (red, green, blue, _) = frame.PixelAt(pixels, x, y);
                Assert.IsTrue(
                    Math.Abs(red - 0x80) <= 1 && Math.Abs(green - 0x80) <= 1 && Math.Abs(blue - 0x80) <= 1,
                    $"({x},{y}) is ({red},{green},{blue}); a grey stroke on grey must be invisible");
            }
        }
    }

    [TestMethod]
    public void TextIsDrawnWithSolidStemsAndAntiAliasedEdges()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);
        frame.Renderer.RegisterMsdfFont(font);

        var pixels = frame.Render(OpaqueBlack, r => r.DrawText(font, "I", 16, 0, 56f, new Vector4(1f, 1f, 1f, 1f)));

        var solid = 0;
        var partial = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var value = pixels[i + 2];
            if (value == 255)
            {
                solid++;
            }
            else if (value > 0)
            {
                partial++;
            }
        }
        Assert.IsTrue(solid > 40, $"only {solid} fully covered pixels; the stem should be solid");
        Assert.IsTrue(partial > 0, "no partially covered pixels; the edges should be anti-aliased");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ACompositedFrameIsIdenticalToTheFrameItWasRenderedFrom(bool opaque)
    {
        // The tab host's path: a frame rendered headless is uploaded to a texture and drawn 1:1 into
        // the window. With one format choice for both and premultiplied texels, nothing may change.
        using var frame = GpuFrame.CreateOrSkip(96, 64);
        var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);
        frame.Renderer.RegisterMsdfFont(font);
        var clear = opaque ? new Vector4(0.05f, 0.05f, 0.06f, 1f) : Vector4.Zero;

        var original = frame.Render(clear, r =>
        {
            r.DrawRoundedRect(4, 4, 88, 56, 10f, 2f, new Vector4(0.2f, 0.4f, 0.9f, 0.6f), Color.Parse("#f59e0b"));
            r.DrawRectangleFilled(30, 10, 40, 20, new Vector4(1f, 1f, 1f, 0.35f));
            r.DrawText(font, "Radiant", 10, 30, 20f, new Vector4(1f, 1f, 1f, 0.9f));
        });

        using var texture = Texture2D.Create(frame.Renderer, frame.Width, frame.Height, TextureFormat.Bgra8UnormSrgb);
        texture.Update(original);
        var composited = frame.Render(Vector4.Zero, r => r.DrawImage(texture, 0, 0, frame.Width, frame.Height));

        var worst = 0;
        for (var i = 0; i < original.Length; i++)
        {
            worst = Math.Max(worst, Math.Abs(original[i] - composited[i]));
        }
        Assert.IsTrue(worst <= 1, $"a channel differs by {worst}");
    }

    private static (int, int, int, int) ToInts((byte R, byte G, byte B, byte A) pixel) =>
        (pixel.R, pixel.G, pixel.B, pixel.A);

    private static void AssertChannel(int expected, byte actual) =>
        Assert.IsTrue(Math.Abs(expected - actual) <= 1, $"expected {expected}±1, got {actual}");
}
