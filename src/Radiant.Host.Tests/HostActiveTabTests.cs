using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host.Tests;

[TestClass]
public sealed class HostActiveTabTests
{
    [TestMethod]
    [DoNotParallelize]
    public void WriteThenReadRoundTripsTheActiveTabName()
    {
        var savedRoot = InstanceRegistry.RootDir;
        var testRoot = Path.Combine(Path.GetTempPath(), $"radiant-test-{Guid.NewGuid():N}");
        InstanceRegistry.RootDir = testRoot;
        try
        {
            HostActiveTab.Write("radiant-host", "tab-42");
            Assert.AreEqual("tab-42", HostActiveTab.Read("radiant-host"));
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
    public void ReadReturnsNullWhenUnset()
    {
        var savedRoot = InstanceRegistry.RootDir;
        var testRoot = Path.Combine(Path.GetTempPath(), $"radiant-test-{Guid.NewGuid():N}");
        InstanceRegistry.RootDir = testRoot;
        try
        {
            Assert.IsNull(HostActiveTab.Read("radiant-host-never-written"));
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
    public void WriteNullOrEmptyReadsBackAsNull()
    {
        var savedRoot = InstanceRegistry.RootDir;
        var testRoot = Path.Combine(Path.GetTempPath(), $"radiant-test-{Guid.NewGuid():N}");
        InstanceRegistry.RootDir = testRoot;
        try
        {
            HostActiveTab.Write("radiant-host", "tab-1");
            HostActiveTab.Write("radiant-host", null); // all tabs closed → empty marker
            Assert.IsNull(HostActiveTab.Read("radiant-host"));
        }
        finally
        {
            InstanceRegistry.RootDir = savedRoot;
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }
}
