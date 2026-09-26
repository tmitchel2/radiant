using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Color = Radiant.Graphics2D.Color;

namespace Radiant.Tests.Graphics2D;

/// <summary>What transformed drawing looks like once the GPU has drawn it.</summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class RendererTransformGpuTests
{
    private const int Size = 64;
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    private static readonly Color Green = Color.Parse("#00ff00");

    [TestMethod]
    public void ARotatedRectangleCoversItsRotatedFootprint()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        // A wide, short bar through the centre, turned a quarter: it becomes tall and narrow.
        var pixels = frame.Render(Black, r =>
        {
            r.PushTransform(RotateAboutCentre(MathF.PI / 2));
            r.DrawRoundedRectFilled(8, 28, 48, 8, 2f, Green);
            r.PopTransform();
        });

        Assert.AreEqual(0xff, frame.PixelAt(pixels, 32, 12).G, "above the centre, inside the rotated bar");
        Assert.AreEqual(0, frame.PixelAt(pixels, 12, 32).G, "left of the centre, where the unrotated bar was");
    }

    [TestMethod]
    public void AScaledShapeKeepsAOnePixelEdge()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(Black, r =>
        {
            r.PushTransform(Matrix3x2.CreateScale(4f));
            r.DrawDisc(new Vector2(8, 8), 6f, Green);
            r.PopTransform();
        });

        // The disc is now radius 24 at (32, 32). Walking out along a row, the edge is one pixel
        // of partial coverage, not four: anti-aliasing follows the screen, not the shape's units.
        var partial = 0;
        for (var x = 32; x < Size; x++)
        {
            var g = frame.PixelAt(pixels, x, 32).G;
            if (g is > 5 and < 250)
            {
                partial++;
            }
        }
        Assert.AreEqual(0xff, frame.PixelAt(pixels, 32 + 22, 32).G, "inside the scaled disc");
        Assert.IsTrue(partial <= 2, $"{partial} partially covered pixels at the edge");
    }

    [TestMethod]
    public void RotatedTextStillHasSolidStems()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);

        var pixels = frame.Render(Black, r =>
        {
            r.PushTransform(RotateAboutCentre(MathF.PI / 4));
            r.DrawText(font, "I", 24, 4, 56f, Vector4.One);
            r.PopTransform();
        });

        var solid = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 1] == 255)
            {
                solid++;
            }
        }
        Assert.IsTrue(solid > 40, $"only {solid} fully covered pixels");
    }

    private static Matrix3x2 RotateAboutCentre(float radians) =>
        Matrix3x2.CreateRotation(radians, new Vector2(Size / 2f, Size / 2f));
}
