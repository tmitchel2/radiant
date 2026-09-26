using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Color = Radiant.Graphics2D.Color;

namespace Radiant.Tests.Graphics2D;

/// <summary>Gradient fills, checked against the values each interpolation space implies.</summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class RendererGradientGpuTests
{
    private const int Size = 64;
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    [TestMethod]
    [DataRow(GradientInterpolation.Srgb)]
    [DataRow(GradientInterpolation.Linear)]
    [DataRow(GradientInterpolation.Oklab)]
    public void BlackToWhiteFollowsItsInterpolationSpace(GradientInterpolation space)
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var gradient = Gradient.Linear(new Vector2(0, 0), new Vector2(Size, 0), Black, White, space);

        var pixels = frame.Render(Black, r => r.DrawRoundedRectFilled(0, 0, Size, Size, CornerRadii.All(0f), gradient));

        for (var x = 0; x < Size; x++)
        {
            var t = (x + 0.5) / Size;
            // Grey in OKLab has lightness equal to the cube root of linear luminance.
            double expected = space switch
            {
                GradientInterpolation.Srgb => t * 255,
                GradientInterpolation.Linear => SrgbTransfer.ToSrgbByte((float)t),
                _ => SrgbTransfer.ToSrgbByte((float)(t * t * t)),
            };
            var actual = frame.PixelAt(pixels, x, 32).R;
            Assert.IsTrue(Math.Abs(expected - actual) <= 2, $"{space} at x={x}: expected {expected:F1}, got {actual}");
        }
    }

    [TestMethod]
    public void AMiddleStopIsReachedExactly()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var gradient = Gradient.Linear(new Vector2(0, 0), new Vector2(Size, 0),
        [
            new GradientStop(0f, Color.Parse("#ff0000")),
            new GradientStop(0.5f, Color.Parse("#00ff00")),
            new GradientStop(1f, Color.Parse("#0000ff")),
        ]);

        var pixels = frame.Render(Black, r => r.DrawRoundedRectFilled(0, 0, Size, Size, CornerRadii.All(0f), gradient));

        var middle = frame.PixelAt(pixels, 32, 32);
        Assert.IsTrue(middle.G >= 250 && middle.R <= 5 && middle.B <= 5, $"({middle.R},{middle.G},{middle.B})");
        Assert.IsTrue(frame.PixelAt(pixels, 0, 32).R >= 250, "starts red");
        Assert.IsTrue(frame.PixelAt(pixels, Size - 1, 32).B >= 250, "ends blue");
    }

    [TestMethod]
    public void BeforeTheFirstStopAndAfterTheLastTheEndColorsHold()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var gradient = Gradient.Linear(new Vector2(0, 0), new Vector2(Size, 0),
            [new GradientStop(0.25f, Black), new GradientStop(0.75f, White)]);

        var pixels = frame.Render(Black, r => r.DrawRoundedRectFilled(0, 0, Size, Size, CornerRadii.All(0f), gradient));

        for (var x = 0; x < 15; x++)
        {
            Assert.AreEqual(0, frame.PixelAt(pixels, x, 32).R, $"x={x}");
        }
        for (var x = 49; x < Size; x++)
        {
            Assert.AreEqual(255, frame.PixelAt(pixels, x, 32).R, $"x={x}");
        }
    }

    [TestMethod]
    public void ARadialGradientChangesWithDistanceFromItsCenter()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var center = new Vector2(32, 32);
        var gradient = Gradient.Radial(center, 30f, White, Black);

        var pixels = frame.Render(Black, r => r.DrawDisc(center, 30f, gradient));

        Assert.IsTrue(frame.PixelAt(pixels, 32, 32).R >= 245, $"white at the center, got {frame.PixelAt(pixels, 32, 32).R}");
        // The same distance in any direction gives the same color.
        var right = frame.PixelAt(pixels, 32 + 15, 32).R;
        var down = frame.PixelAt(pixels, 32, 32 + 15).R;
        Assert.IsTrue(Math.Abs(right - down) <= 1, $"{right} vs {down}");
        Assert.IsTrue(right is > 100 and < 160, $"half way out is mid-grey in sRGB, got {right}");
    }

    [TestMethod]
    public void FadingFromTransparentKeepsTheHue()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var red = Color.Parse("#ff0000");
        var gradient = Gradient.Linear(new Vector2(0, 0), new Vector2(Size, 0), Color.Transparent, red);

        var pixels = frame.Render(White, r => r.DrawRoundedRectFilled(0, 0, Size, Size, CornerRadii.All(0f), gradient));

        // Over white, a pure red at any alpha keeps the red channel at full. Interpolating straight
        // alpha from transparent *black* would drag it down mid-way.
        for (var x = 0; x < Size; x++)
        {
            Assert.IsTrue(frame.PixelAt(pixels, x, 32).R >= 254, $"x={x} darkened to {frame.PixelAt(pixels, x, 32).R}");
        }
    }

    [TestMethod]
    public void AGradientTurnsWithItsShape()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var gradient = Gradient.Linear(new Vector2(0, 0), new Vector2(Size, 0), Black, White);

        var pixels = frame.Render(Black, r =>
        {
            r.PushTransform(Matrix3x2.CreateRotation(MathF.PI / 2, new Vector2(Size / 2f, Size / 2f)));
            r.DrawRoundedRectFilled(0, 0, Size, Size, CornerRadii.All(0f), gradient);
            r.PopTransform();
        });

        // Left-to-right, turned a quarter clockwise, runs top to bottom.
        Assert.IsTrue(frame.PixelAt(pixels, 32, 2).R < 20, "dark at the top");
        Assert.IsTrue(frame.PixelAt(pixels, 32, Size - 3).R > 235, "light at the bottom");
        Assert.IsTrue(Math.Abs(frame.PixelAt(pixels, 5, 32).R - frame.PixelAt(pixels, 58, 32).R) <= 1, "constant across");
    }
}
