using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

/// <summary>
/// Shape edges blend as they would in sRGB (Renderer2D.SrgbEdges), so a thin border looks as heavy
/// where it curves, spread over partly covered pixels, as where it runs straight and pixel-aligned.
/// </summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class RendererEdgeGpuTests
{
    private const int Size = 100;
    private const float Radius = 24f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    // How dark a region is, as it looks: the sum of each pixel's distance from white in sRGB.
    private static float Ink(GpuFrame frame, byte[] pixels, int left, int top, int right, int bottom)
    {
        var ink = 0f;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                ink += (255 - pixels[(y * frame.Width + x) * 4 + 1]) / 255f;
            }
        }
        return ink;
    }

    // The border's ink per pixel of length along its straight top edge, and round its top-left corner.
    private static (float Straight, float Curved) Weights(bool srgbEdges, Vector4 border, Vector4 fill)
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var pixels = frame.Render(White, r =>
        {
            r.SrgbEdges = srgbEdges;
            r.DrawRoundedRect(10, 10, 80, 80, Radius, 1f, fill, border);
        });
        var straight = Ink(frame, pixels, 40, 5, 60, 16) / 20f;
        // The corner's quarter circle runs from (10, 34) to (34, 10), 1 px in from the outside edge.
        var curved = Ink(frame, pixels, 5, 5, 34, 34) / (MathF.PI / 2f * (Radius - 0.5f));
        return (straight, curved);
    }

    [TestMethod]
    public void AThinBorderIsAsHeavyRoundItsCornersAsAlongItsSides()
    {
        var grey = new Vector4(0.2f, 0.2f, 0.2f, 1f);

        var (straight, curved) = Weights(true, grey, White);
        var (_, linear) = Weights(false, grey, White);

        Assert.AreEqual(straight, curved, straight * 0.05f, $"straight {straight}, curved {curved}");
        Assert.IsTrue(linear < straight * 0.85f, $"blended in linear light, the corner fades: {linear} against {straight}");
    }

    [TestMethod]
    public void ALightBorderOnADarkFillIsEvenToo()
    {
        var light = new Vector4(0.6f, 0.6f, 0.6f, 1f);
        var dark = new Vector4(0.02f, 0.02f, 0.02f, 1f);
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var pixels = frame.Render(dark, r => r.DrawRoundedRect(10, 10, 80, 80, Radius, 1f, dark, light));

        // Measured as brightness above the dark ground.
        float Light(int left, int top, int right, int bottom)
        {
            var sum = 0f;
            for (var y = top; y < bottom; y++)
            {
                for (var x = left; x < right; x++)
                {
                    sum += pixels[(y * frame.Width + x) * 4 + 1] / 255f;
                }
            }
            return sum;
        }
        var ground = Light(0, 0, 4, 4) / 16f;
        var straight = (Light(40, 5, 60, 16) - ground * 20 * 11) / 20f;
        var curved = (Light(5, 5, 34, 34) - ground * 29 * 29) / (MathF.PI / 2f * (Radius - 0.5f));

        Assert.AreEqual(straight, curved, straight * 0.05f, $"straight {straight}, curved {curved}");
    }
}
