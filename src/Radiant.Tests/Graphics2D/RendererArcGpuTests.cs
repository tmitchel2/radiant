using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

/// <summary>What arcs look like once drawn.</summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class RendererArcGpuTests
{
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 White = Vector4.One;
    private static readonly Vector2 Centre = new(32, 32);

    private static byte At(GpuFrame frame, byte[] pixels, float angle, float radius = 20f)
    {
        var x = (int)(Centre.X + MathF.Cos(angle) * radius);
        var y = (int)(Centre.Y + MathF.Sin(angle) * radius);
        return frame.PixelAt(pixels, x, y).G;
    }

    [TestMethod]
    public void AQuarterArcCoversItsQuarterClockwiseFromItsStart()
    {
        using var frame = GpuFrame.CreateOrSkip(64, 64);

        // From pointing right, a quarter turn clockwise on screen: to pointing down.
        var pixels = frame.Render(Black, r => r.DrawArc(Centre, 20f, 6f, 0f, MathF.PI / 2f, White));

        Assert.AreEqual(255, At(frame, pixels, MathF.PI / 4f), "the middle of the arc, down and to the right");
        Assert.AreEqual(0, At(frame, pixels, MathF.PI), "the opposite side");
        Assert.AreEqual(0, At(frame, pixels, -MathF.PI / 4f), "up and to the right, before the start");
        Assert.AreEqual(0, At(frame, pixels, MathF.PI / 4f, 10f), "inside the ring");
    }

    [TestMethod]
    public void AnArcHasRoundEnds()
    {
        using var frame = GpuFrame.CreateOrSkip(64, 64);

        var pixels = frame.Render(Black, r => r.DrawArc(Centre, 20f, 8f, 0f, MathF.PI / 2f, White));

        // Just before the start, still within the cap's half-circle of radius 4.
        Assert.IsTrue(frame.PixelAt(pixels, 52, 29).G > 200);
    }

    [TestMethod]
    public void AFullSweepIsARing()
    {
        using var frame = GpuFrame.CreateOrSkip(64, 64);

        var pixels = frame.Render(Black, r => r.DrawArc(Centre, 20f, 6f, 1f, MathF.Tau, White));

        foreach (var angle in new[] { 0f, 1.5f, 3f, 4.5f, 6f })
        {
            Assert.AreEqual(255, At(frame, pixels, angle), $"{angle}");
        }
    }
}
