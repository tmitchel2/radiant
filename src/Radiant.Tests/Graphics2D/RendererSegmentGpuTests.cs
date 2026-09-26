using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

/// <summary>Segments are anti-aliased strokes with round ends, at any angle.</summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class RendererSegmentGpuTests
{
    private const int Size = 64;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);

    private static byte Green(GpuFrame frame, byte[] pixels, int x, int y) => pixels[(y * frame.Width + x) * 4 + 1];

    [TestMethod]
    public void ADiagonalSegmentIsSolidInTheMiddleAndSmoothAtItsEdges()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var pixels = frame.Render(White, r => r.DrawSegment(new Vector2(10, 10), new Vector2(54, 54), 4f, Black));

        var partial = 0;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var g = Green(frame, pixels, x, y);
                if (g is > 20 and < 235)
                {
                    partial++;
                }
            }
        }

        Assert.IsTrue(Green(frame, pixels, 32, 32) < 10, "on the line");
        Assert.IsTrue(Green(frame, pixels, 40, 24) > 245, "well off it");
        Assert.IsTrue(partial > 40, $"{partial} partly covered pixels along the edges: anti-aliased, not stair-stepped");
    }

    [TestMethod]
    public void ASegmentsEndsAreRound()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        var pixels = frame.Render(White, r => r.DrawSegment(new Vector2(20, 32), new Vector2(44, 32), 16f, Black));

        Assert.IsTrue(Green(frame, pixels, 14, 32) < 10, "the cap reaches half the width past the end");
        Assert.IsTrue(Green(frame, pixels, 13, 25) > 200, "but not into the corner a square cap would fill");
        Assert.IsTrue(Green(frame, pixels, 32, 25) < 10, "the stroke is 16 wide");
    }
}
