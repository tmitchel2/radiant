using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

/// <summary>
/// Soft shadows against the exact answer. A Gaussian-blurred box with square corners has a closed
/// form — the product of two one-dimensional Gaussian integrals — so the shader's analytic
/// approximation can be checked pixel by pixel. White at full alpha over a transparent target makes
/// the read-back alpha the shadow's coverage.
/// </summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class RendererShadowGpuTests
{
    private const int Size = 64;
    private static readonly Vector4 White = Vector4.One;

    [TestMethod]
    public void ASquareCorneredShadowMatchesTheExactGaussianBlur()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        const float blur = 8f; // sigma 4

        var pixels = frame.Render(Vector4.Zero, r => r.DrawShadow(16, 16, 32, 32, 0f, blur, White));

        var worst = 0.0;
        var at = "";
        for (var y = 0; y < Size; y += 3)
        {
            for (var x = 0; x < Size; x += 3)
            {
                var expected = ExactBoxBlur(x + 0.5, y + 0.5, 16, 16, 32, 32, blur / 2) * 255;
                var actual = frame.PixelAt(pixels, x, y).A;
                if (Math.Abs(expected - actual) > worst)
                {
                    worst = Math.Abs(expected - actual);
                    at = $"({x},{y}) expected {expected:F1}, got {actual}";
                }
            }
        }
        Assert.IsTrue(worst <= 4, $"worst error {worst:F1}/255 at {at}");
    }

    [TestMethod]
    public void AShadowFadesSteadilyAwayFromItsBox()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(Vector4.Zero, r => r.DrawShadow(16, 16, 32, 32, 8f, 12f, White));

        var previous = 256;
        for (var x = 32; x < Size; x++)
        {
            int alpha = frame.PixelAt(pixels, x, 32).A;
            Assert.IsTrue(alpha <= previous, $"coverage rose from {previous} to {alpha} at x={x}");
            previous = alpha;
        }
        Assert.IsTrue(frame.PixelAt(pixels, 32, 32).A >= 250, "full under the middle of the box");
        Assert.IsTrue(previous <= 3, "gone three standard deviations out");
    }

    [TestMethod]
    public void RoundedCornersSoftenTheShadowsCorners()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var square = frame.Render(Vector4.Zero, r => r.DrawShadow(16, 16, 32, 32, 0f, 4f, White));
        var rounded = frame.Render(Vector4.Zero, r => r.DrawShadow(16, 16, 32, 32, 12f, 4f, White));

        // Just inside the corner, a rounded box covers much less.
        Assert.IsTrue(rounded[(17 * Size + 17) * 4 + 3] + 60 < square[(17 * Size + 17) * 4 + 3]);
        // Mid-edge, where the corner radius doesn't reach, they agree.
        Assert.IsTrue(Math.Abs(rounded[(16 * Size + 32) * 4 + 3] - square[(16 * Size + 32) * 4 + 3]) <= 2);
    }

    [TestMethod]
    public void OffsetAndSpreadMoveAndGrowTheShadow()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        var pixels = frame.Render(Vector4.Zero, r =>
            r.DrawShadow(20, 20, 16, 16, 0f, 0f, White, offset: new Vector2(8, 8), spread: 4));

        // Hard edges (no blur): the box 20..36 grows to 16..40, then moves to 24..48.
        Assert.AreEqual(0, frame.PixelAt(pixels, 23, 30).A);
        Assert.AreEqual(255, frame.PixelAt(pixels, 24, 30).A);
        Assert.AreEqual(255, frame.PixelAt(pixels, 47, 30).A);
        Assert.AreEqual(0, frame.PixelAt(pixels, 48, 30).A);
    }

    [TestMethod]
    public void ASurfaceDrawnAfterItsShadowSitsOnTopOfIt()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var surface = new Vector4(1f, 0f, 0f, 1f);

        var pixels = frame.Render(new Vector4(1f, 1f, 1f, 1f), r =>
        {
            r.DrawShadow(16, 16, 32, 32, 8f, 8f, new Vector4(0f, 0f, 0f, 0.5f), offset: new Vector2(0, 4));
            r.DrawRoundedRectFilled(16, 16, 32, 32, 8f, surface);
        });

        Assert.AreEqual((255, 0, 0), (frame.PixelAt(pixels, 32, 32).R, frame.PixelAt(pixels, 32, 32).G, frame.PixelAt(pixels, 32, 32).B));
        Assert.IsTrue(frame.PixelAt(pixels, 32, 52).R < 250, "the shadow shows below the surface");
    }

    // Coverage of the box (left, top, width, height) blurred by a Gaussian of deviation sigma, at a
    // point: the product of the blurred extents along x and y.
    private static double ExactBoxBlur(double px, double py, double left, double top, double width, double height, double sigma) =>
        BlurredInterval(px, left, left + width, sigma) * BlurredInterval(py, top, top + height, sigma);

    private static double BlurredInterval(double p, double from, double to, double sigma) =>
        Phi((to - p) / sigma) - Phi((from - p) / sigma);

    private static double Phi(double z) => 0.5 * (1 + Erf(z / Math.Sqrt(2)));

    // Abramowitz and Stegun 7.1.26, good to 1.5e-7: far tighter than the shader's approximation.
    private static double Erf(double x)
    {
        var sign = Math.Sign(x);
        x = Math.Abs(x);
        var t = 1 / (1 + 0.3275911 * x);
        var y = 1 - ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return sign * y;
    }
}
