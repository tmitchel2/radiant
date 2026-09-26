using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

/// <summary>How draws group into batches: as few as order allows.</summary>
[TestClass]
public class DrawBatchTests
{
    private static readonly Vector4 Red = new(1f, 0f, 0f, 1f);

    // Recording needs no GPU: batches are built as draws are made.
    private static Renderer2D NewFrame() => new();

    [TestMethod]
    public void ManyRectsOfOneKindAreOneBatch()
    {
        var r = NewFrame();

        for (var i = 0; i < 100; i++)
        {
            r.DrawRectangleFilled(i, i, 10, 10, Red);
        }

        Assert.AreEqual(1, r.BatchCount);
    }

    [TestMethod]
    public void ManyRoundedRectsAreOneBatch()
    {
        var r = NewFrame();

        for (var i = 0; i < 50; i++)
        {
            r.DrawRoundedRectFilled(i, i, 10, 10, 3f, Red);
        }

        Assert.AreEqual(1, r.BatchCount);
    }

    [TestMethod]
    public void SwitchingKindStartsANewBatchEachTime()
    {
        var r = NewFrame();

        r.DrawRectangleFilled(0, 0, 10, 10, Red);
        r.DrawRoundedRectFilled(0, 0, 10, 10, 3f, Red);
        r.DrawRectangleFilled(0, 0, 10, 10, Red);

        Assert.AreEqual(3, r.BatchCount);
    }

    [TestMethod]
    public void OutlinesBetweenFillsKeepTheirPlace()
    {
        var r = NewFrame();

        r.DrawRectangleFilled(0, 0, 10, 10, Red);
        r.DrawRectangleOutline(0, 0, 10, 10, Red);
        r.DrawRectangleFilled(0, 0, 10, 10, Red);

        Assert.AreEqual(3, r.BatchCount);
    }
}
