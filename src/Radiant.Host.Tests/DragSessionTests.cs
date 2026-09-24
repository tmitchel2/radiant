using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host.Tests;

[TestClass]
public sealed class DragSessionTests
{
    private static string UseTempRoot(out string testRoot)
    {
        var saved = InstanceRegistry.RootDir;
        testRoot = Path.Combine(Path.GetTempPath(), $"radiant-test-{Guid.NewGuid():N}");
        InstanceRegistry.RootDir = Path.Combine(testRoot, "instances");
        return saved;
    }

    [TestMethod]
    [DoNotParallelize]
    public void WriteThenReadRoundTrips()
    {
        var saved = UseTempRoot(out var testRoot);
        try
        {
            DragSession.Write(new DragSessionState
            {
                Active = true,
                CursorX = 12.5f,
                CursorY = 34.5f,
                TabName = "renderer-1",
                FramesPath = "/p/frames.bin",
                SourceHost = "radiant-host",
                Label = "wt-a",
                TargetHost = "radiant-host-99-1",
                PreviewPath = "/p/merge-preview.bin",
            });

            var read = DragSession.Read();
            Assert.IsNotNull(read);
            Assert.IsTrue(read!.Active);
            Assert.AreEqual(12.5f, read.CursorX, 1e-4f);
            Assert.AreEqual(34.5f, read.CursorY, 1e-4f);
            Assert.AreEqual("renderer-1", read.TabName);
            Assert.AreEqual("wt-a", read.Label);
            Assert.AreEqual("radiant-host-99-1", read.TargetHost);
            Assert.AreEqual("/p/merge-preview.bin", read.PreviewPath);
        }
        finally
        {
            InstanceRegistry.RootDir = saved;
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void ClearWritesInactiveState()
    {
        var saved = UseTempRoot(out var testRoot);
        try
        {
            DragSession.Write(new DragSessionState { Active = true });
            DragSession.Clear();

            var read = DragSession.Read();
            Assert.IsNotNull(read);
            Assert.IsFalse(read!.Active);
        }
        finally
        {
            InstanceRegistry.RootDir = saved;
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void ReadMissingReturnsNull()
    {
        var saved = UseTempRoot(out var testRoot);
        try
        {
            Assert.IsNull(DragSession.Read());
        }
        finally
        {
            InstanceRegistry.RootDir = saved;
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }
}
