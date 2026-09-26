using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

/// <summary>
/// Opacity layers fade a group as one image. Over white, 50% blue is (188, 188, 255) and 50% red is
/// (255, 188, 188) (half the light, in linear blending).
/// </summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class RendererLayerGpuTests
{
    private const int Size = 64;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Red = new(1f, 0f, 0f, 1f);
    private static readonly Vector4 Blue = new(0f, 0f, 1f, 1f);
    private static readonly Vector4 Green = new(0f, 1f, 0f, 1f);

    [TestMethod]
    [DataRow(1u)]
    [DataRow(4u)]
    public void OverlappingShapesInALayerDoNotShowThroughEachOther(uint samples)
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size, sampleCount: samples);

        var pixels = frame.Render(White, r =>
        {
            r.PushLayer(0.5f);
            r.DrawRectangleFilled(0, 0, 40, Size, Red);
            r.DrawRoundedRectFilled(24, 0, 40, Size, 0f, Blue);
            r.PopLayer();
        });

        AssertPixel(frame, pixels, 8, 32, (255, 188, 188), "red alone, at half opacity");
        AssertPixel(frame, pixels, 32, 32, (188, 188, 255), "where blue covers red, only blue: the red below must not show through");
        AssertPixel(frame, pixels, 56, 32, (188, 188, 255), "blue alone");
    }

    [TestMethod]
    public void AFullyOpaqueLayerChangesNothing()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);

        void Scene(Renderer2D r)
        {
            r.DrawRoundedRectFilled(4, 4, 56, 56, 12f, Blue);
            r.DrawText(font, "Hi", 10, 10, 30f, White);
        }

        var direct = frame.Render(White, Scene);
        var layered = frame.Render(White, r =>
        {
            r.PushLayer(1f);
            Scene(r);
            r.PopLayer();
        });

        for (var i = 0; i < direct.Length; i++)
        {
            Assert.IsTrue(Math.Abs(direct[i] - layered[i]) <= 1, $"byte {i}: {direct[i]} vs {layered[i]}");
        }
    }

    [TestMethod]
    public void NestedLayersMultiplyTheirOpacities()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(White, r =>
        {
            r.PushLayer(0.5f);
            r.PushLayer(0.5f);
            r.DrawRectangleFilled(0, 0, Size, Size, Red);
            r.PopLayer();
            r.PopLayer();
        });

        // A quarter of red over white: the other channels keep three quarters of the light.
        var quarter = SrgbTransfer.ToSrgbByte(0.75f);
        AssertPixel(frame, pixels, 32, 32, (255, quarter, quarter), "0.5 x 0.5");
    }

    [TestMethod]
    public void DrawsAroundALayerStackInOrderWithIt()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(White, r =>
        {
            r.DrawRectangleFilled(0, 0, Size, 16, Green);   // under the layer
            r.PushLayer(0.5f);
            r.DrawRectangleFilled(0, 0, Size, Size, Red);
            r.PopLayer();
            r.DrawRectangleFilled(0, 48, Size, 16, Green);  // over the layer
        });

        AssertPixel(frame, pixels, 32, 8, (188, 188, 0), "green under half red");
        AssertPixel(frame, pixels, 32, 56, (0, 255, 0), "green drawn after the layer covers it");
    }

    [TestMethod]
    public void ATransformAroundALayerMovesItsContentOnce()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(White, r =>
        {
            r.PushTransform(Matrix3x2.CreateTranslation(32, 0));
            r.PushLayer(1f);
            r.DrawRectangleFilled(0, 0, 16, Size, Red);
            r.PopLayer();
            r.PopTransform();
        });

        AssertPixel(frame, pixels, 8, 32, (255, 255, 255), "not at the original place");
        AssertPixel(frame, pixels, 40, 32, (255, 0, 0), "moved once, by 32");
    }

    [TestMethod]
    public void ClipsInsideALayerStillApply()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(White, r =>
        {
            r.PushLayer(1f);
            r.PushClip(8, 8, 48, 48, 16f);
            r.DrawRectangleFilled(0, 0, Size, Size, Red);
            r.PopClip();
            r.PopLayer();
        });

        AssertPixel(frame, pixels, 9, 9, (255, 255, 255), "corner cut");
        AssertPixel(frame, pixels, 32, 32, (255, 0, 0), "middle kept");
    }

    [TestMethod]
    public void ALayerLeftOpenIsStillDrawn()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(White, r =>
        {
            r.PushLayer(0.5f);
            r.DrawRectangleFilled(0, 0, Size, Size, Red);
        });

        AssertPixel(frame, pixels, 32, 32, (255, 188, 188), "closed by EndFrame");
    }

    [TestMethod]
    public void LayersNeedTheAttachmentSize()
    {
        var renderer = new Renderer2D();

        Assert.ThrowsException<InvalidOperationException>(() => renderer.PushLayer(0.5f));
    }

    private static void AssertPixel(GpuFrame frame, byte[] pixels, int x, int y, (int R, int G, int B) expected, string message)
    {
        var (r, g, b, _) = frame.PixelAt(pixels, x, y);
        Assert.IsTrue(
            Math.Abs(r - expected.R) <= 1 && Math.Abs(g - expected.G) <= 1 && Math.Abs(b - expected.B) <= 1,
            $"({x},{y}) is ({r},{g},{b}), expected {expected}: {message}");
    }
}
