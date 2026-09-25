using Radiant.Host.AgentControlProtocol;
using Radiant.Host.Ipc.Recents;

namespace Radiant.Host.Tests;

/// <summary>
/// The identity is process-wide and <see cref="RadiantAppIdentity.Use"/> repoints the registry, so these
/// run alone and put the previous identity back.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class RadiantAppIdentityTests
{
    private static readonly string[] s_bareTab = ["--attach", "--name", "tab-1"];
    private static readonly string[] s_tabWithPath = ["--attach", "--name", "tab-1", "--scene", "/docs/a.txt"];
    private static readonly byte[] s_pngSignature = [0x89, 0x50, 0x4E, 0x47];

    private static RadiantAppIdentity App(string dataDirectory) => RadiantAppIdentity.Default with
    {
        Name = "My App",
        DataDirectory = dataDirectory,
        InstancePrefix = "myapp",
        EnvironmentPrefix = "MYAPP_IDENTITY_TEST",
    };

    [TestMethod]
    public void DerivedNamesComeFromThePrefix()
    {
        var app = App("/data/myapp");

        Assert.AreEqual("myapp-host", app.PrimaryHostName);
        Assert.AreEqual("myapp-drag-overlay", app.OverlayInstanceName);
        Assert.AreEqual(Path.Combine("/data/myapp", "instances"), app.InstancesDirectory);
        Assert.AreEqual($"myapp-{Environment.ProcessId}", app.GenerateInstanceName());
    }

    [TestMethod]
    public void UsePointsTheRegistryAndTheRecentsAtTheDataDirectory()
    {
        var saved = RadiantAppIdentity.Current;
        var savedRoot = InstanceRegistry.RootDir;
        var savedStore = RecentFilesStore.StoreDir;
        try
        {
            var app = App("/data/myapp");
            RadiantAppIdentity.Use(app);

            Assert.AreSame(app, RadiantAppIdentity.Current);
            Assert.AreEqual(app.InstancesDirectory, InstanceRegistry.RootDir);
            Assert.AreEqual("/data/myapp", RecentFilesStore.StoreDir);
            Assert.AreEqual("myapp-host", TabOwnership.PrimaryHostName);
        }
        finally
        {
            RadiantAppIdentity.Use(saved);
            InstanceRegistry.RootDir = savedRoot;
            RecentFilesStore.StoreDir = savedStore;
        }
    }

    [TestMethod]
    public void TheDefaultIsAGenericRadiantApplication()
    {
        var app = RadiantAppIdentity.Default;

        Assert.AreEqual("Radiant", app.Name);
        Assert.AreEqual("radiant", app.InstancePrefix);
        Assert.AreEqual("RADIANT", app.EnvironmentPrefix);
        Assert.AreEqual(".radiant", Path.GetFileName(app.DataDirectory));
        Assert.IsEmpty(app.OpenFileExtensions);
        Assert.IsNull(app.NewDocument);
        Assert.IsNull(app.WorktreeProject);
    }

    [TestMethod]
    public void EnvironmentIsReadsThePrefixedSwitch()
    {
        var app = App("/data/myapp");
        Environment.SetEnvironmentVariable("MYAPP_IDENTITY_TEST_DRAG_DEBUG", "1");
        try
        {
            Assert.IsTrue(app.EnvironmentIs("DRAG_DEBUG", "1"));
            Assert.IsFalse(app.EnvironmentIs("DRAG_DEBUG", "0"));
            Assert.IsFalse(RadiantAppIdentity.Default.EnvironmentIs("DRAG_DEBUG", "1"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MYAPP_IDENTITY_TEST_DRAG_DEBUG", null);
        }
    }

    [TestMethod]
    public void RelaunchAsTabAttachesUnderTheNameAndForwardsThePath()
    {
        var bare = RadiantAppIdentity.RelaunchAsTab("tab-1", path: null).ArgumentList.ToArray();
        var withPath = RadiantAppIdentity.RelaunchAsTab("tab-1", "/docs/a.txt").ArgumentList.ToArray();

        CollectionAssert.AreEqual(s_bareTab, bare[^3..]);
        CollectionAssert.AreEqual(s_tabWithPath, withPath[^5..]);
    }

    [TestMethod]
    public void TheDefaultDockIconIsAnEmbeddedPng()
    {
        var png = RadiantAppIdentity.Default.DockIcon();

        Assert.IsNotNull(png);
        CollectionAssert.AreEqual(s_pngSignature, png[..4]);
    }
}
