using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host.Tests;

[TestClass]
public sealed class HostWindowBoundsTests
{
    [TestMethod]
    [DoNotParallelize]
    public void WriteThenReadRoundTrips()
    {
        var savedRoot = InstanceRegistry.RootDir;
        var testRoot = Path.Combine(Path.GetTempPath(), $"radiant-test-{Guid.NewGuid():N}");
        InstanceRegistry.RootDir = testRoot;
        try
        {
            var bounds = new WindowBounds { X = 100, Y = 50, Width = 1280, Height = 748, StripHeight = 28f };
            HostWindowBounds.Write("radiant-host", bounds);

            var read = HostWindowBounds.Read("radiant-host");
            Assert.IsNotNull(read);
            Assert.AreEqual(100, read!.X);
            Assert.AreEqual(50, read.Y);
            Assert.AreEqual(1280, read.Width);
            Assert.AreEqual(748, read.Height);
            Assert.AreEqual(28f, read.StripHeight, 1e-4f);
        }
        finally
        {
            InstanceRegistry.RootDir = savedRoot;
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void ReadMissingReturnsNull()
    {
        var savedRoot = InstanceRegistry.RootDir;
        var testRoot = Path.Combine(Path.GetTempPath(), $"radiant-test-{Guid.NewGuid():N}");
        InstanceRegistry.RootDir = testRoot;
        try
        {
            Assert.IsNull(HostWindowBounds.Read("never-written"));
        }
        finally
        {
            InstanceRegistry.RootDir = savedRoot;
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public void ContainsStripPreferredOverWindowBody()
    {
        var b = new WindowBounds { X = 0, Y = 0, Width = 200, Height = 100, StripHeight = 28f };
        Assert.IsTrue(b.ContainsStrip(10, 5));
        Assert.IsFalse(b.ContainsStrip(10, 40)); // below strip band
        Assert.IsTrue(b.Contains(10, 40));        // still in the window body
        Assert.IsFalse(b.Contains(250, 40));      // outside the window
    }

    [TestMethod]
    public void ContainsStripWithinGrowsTheBandByMargin()
    {
        var b = new WindowBounds { X = 0, Y = 0, Width = 200, Height = 100, StripHeight = 28f };
        // 0 margin == strict strip band.
        Assert.IsFalse(b.ContainsStripWithin(10, 40, 0f));
        // Just below the strip but within the margin (40 < 28 + 24) → counts as "over the strip".
        Assert.IsTrue(b.ContainsStripWithin(10, 40, 24f));
        // Beyond the margin → no longer over the strip (60 >= 28 + 24).
        Assert.IsFalse(b.ContainsStripWithin(10, 60, 24f));
        // Horizontal cushion also applies (just left of the window within the margin).
        Assert.IsTrue(b.ContainsStripWithin(-10, 10, 24f));
        Assert.IsFalse(b.ContainsStripWithin(-30, 10, 24f));
    }
}
