namespace Radiant.Host.Tests;

[TestClass]
public sealed class ThumbnailScalerTests
{
    [TestMethod]
    public void FitPreservesAspectWithinBox()
    {
        // 1280x720 (16:9) into a 192x120 box is width-limited: 192 x 108.
        var (w, h) = ThumbnailScaler.Fit(1280, 720, 192, 120);
        Assert.AreEqual(192, w);
        Assert.AreEqual(108, h);
    }

    [TestMethod]
    public void FitHeightLimitedWhenTall()
    {
        var (w, h) = ThumbnailScaler.Fit(100, 400, 192, 120);
        Assert.AreEqual(120, h);
        Assert.AreEqual(30, w);
    }

    [TestMethod]
    public void FitNeverReturnsZeroDimension()
    {
        var (w, h) = ThumbnailScaler.Fit(4000, 2, 192, 120);
        Assert.IsTrue(w >= 1);
        Assert.IsTrue(h >= 1);
    }

    [TestMethod]
    public void DownscaleHalvesAndAveragesUniformColour()
    {
        // 2x2 solid BGRA (b=10,g=20,r=30,a=40) → 1x1 average is the same colour.
        var src = new byte[2 * 2 * 4];
        for (var p = 0; p < 4; p++)
        {
            src[p * 4] = 10;
            src[(p * 4) + 1] = 20;
            src[(p * 4) + 2] = 30;
            src[(p * 4) + 3] = 40;
        }
        var dst = new byte[1 * 1 * 4];
        ThumbnailScaler.Downscale(src, 2, 2, dst, 1, 1);
        Assert.AreEqual(10, dst[0]);
        Assert.AreEqual(20, dst[1]);
        Assert.AreEqual(30, dst[2]);
        Assert.AreEqual(40, dst[3]);
    }

    [TestMethod]
    public void DownscaleAveragesDistinctPixels()
    {
        // 2x1 row: blue-channel 0 and 100 → 1x1 averages to 50.
        var src = new byte[2 * 1 * 4];
        src[0] = 0;
        src[4] = 100;
        var dst = new byte[4];
        ThumbnailScaler.Downscale(src, 2, 1, dst, 1, 1);
        Assert.AreEqual(50, dst[0]);
    }

    [TestMethod]
    public void DownscaleRejectsTooSmallDestination()
    {
        var src = new byte[4 * 4 * 4];
        var dst = new byte[3]; // < 1*1*4
        Assert.ThrowsExactly<ArgumentException>(() => ThumbnailScaler.Downscale(src, 4, 4, dst, 1, 1));
    }
}
