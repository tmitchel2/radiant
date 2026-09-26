using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

/// <summary>Frames reuse the renderer's buffers; each must still show exactly its own drawing.</summary>
[TestClass]
[TestCategory(GpuFrame.Category)]
public class RendererFrameGpuTests
{
    private const int Size = 64;
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 Red = new(1f, 0f, 0f, 1f);
    private static readonly Vector4 Green = new(0f, 1f, 0f, 1f);

    [TestMethod]
    public void EachFrameShowsOnlyItsOwnDrawing()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);

        frame.Render(Black, r => r.DrawRectangleFilled(0, 0, 32, 32, Red));
        var second = frame.Render(Black, r => r.DrawRoundedRectFilled(32, 32, 32, 32, 0f, Green));

        Assert.AreEqual(0, frame.PixelAt(second, 16, 16).R, "the first frame's rect is gone");
        Assert.AreEqual(255, frame.PixelAt(second, 48, 48).G);
    }

    [TestMethod]
    public void AFrameFarLargerThanTheLastIsDrawnInFull()
    {
        using var frame = GpuFrame.CreateOrSkip(Size, Size);
        frame.Render(Black, r => r.DrawRectangleFilled(0, 0, 1, 1, Red));

        // 4,096 one-pixel rects: far more vertex data than the first frame's buffers hold.
        var pixels = frame.Render(Black, r =>
        {
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    r.DrawRectangleFilled(x, y, 1, 1, (x + y) % 2 == 0 ? Red : Green);
                }
            }
        });

        Assert.AreEqual(255, frame.PixelAt(pixels, 0, 0).R);
        Assert.AreEqual(255, frame.PixelAt(pixels, 63, 62).G);
        Assert.AreEqual(255, frame.PixelAt(pixels, 63, 63).R);
    }
}
