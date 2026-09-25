using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host.Tests;

[TestClass]
public sealed class TabOwnershipTests
{
    private static string Primary => TabOwnership.PrimaryHostName;
    private const string Secondary = "radiant-host-123-1";

    private static bool Live(string _) => true;
    private static bool Dead(string _) => false;

    [TestMethod]
    public void PrimaryAdoptsUnownedTab()
    {
        Assert.IsTrue(TabOwnership.IsOwnedBy(null, Primary));
        Assert.IsTrue(TabOwnership.IsOwnedBy("", Primary));
    }

    [TestMethod]
    public void PrimaryAdoptsTabOwnedByItself()
    {
        Assert.IsTrue(TabOwnership.IsOwnedBy(Primary, Primary));
    }

    [TestMethod]
    public void PrimaryDoesNotAdoptTabOwnedByAnotherHost()
    {
        Assert.IsFalse(TabOwnership.IsOwnedBy(Secondary, Primary));
    }

    [TestMethod]
    public void SecondaryAdoptsOnlyItsOwnTab()
    {
        Assert.IsTrue(TabOwnership.IsOwnedBy(Secondary, Secondary));
    }

    [TestMethod]
    public void SecondaryDoesNotAdoptUnownedTab()
    {
        Assert.IsFalse(TabOwnership.IsOwnedBy(null, Secondary));
        Assert.IsFalse(TabOwnership.IsOwnedBy("", Secondary));
    }

    [TestMethod]
    public void SecondaryDoesNotAdoptPrimaryOwnedTab()
    {
        Assert.IsFalse(TabOwnership.IsOwnedBy(Primary, Secondary));
    }

    [TestMethod]
    public void IsPrimaryRecognisesOnlyTheDefaultName()
    {
        Assert.IsTrue(TabOwnership.IsPrimary(Primary));
        Assert.IsFalse(TabOwnership.IsPrimary(Secondary));
    }

    [TestMethod]
    public void PrimaryReclaimsTabWhoseOwnerHostIsDead()
    {
        Assert.IsTrue(TabOwnership.IsOrphaned(Secondary, Primary, Dead));
    }

    [TestMethod]
    public void PrimaryDoesNotReclaimTabWhoseOwnerHostIsLive()
    {
        Assert.IsFalse(TabOwnership.IsOrphaned(Secondary, Primary, Live));
    }

    [TestMethod]
    public void PrimaryDoesNotReclaimItsOwnOrUnownedTab()
    {
        Assert.IsFalse(TabOwnership.IsOrphaned(Primary, Primary, Dead));
        Assert.IsFalse(TabOwnership.IsOrphaned(null, Primary, Dead));
        Assert.IsFalse(TabOwnership.IsOrphaned("", Primary, Dead));
    }

    [TestMethod]
    public void SecondaryNeverReclaims()
    {
        // Only the primary reclaims, even if the named owner is dead.
        Assert.IsFalse(TabOwnership.IsOrphaned("radiant-host-999-2", Secondary, Dead));
    }

    [TestMethod]
    [DoNotParallelize]
    public void ClearOwnerRemovesTheMarker()
    {
        var savedRoot = InstanceRegistry.RootDir;
        var testRoot = Path.Combine(Path.GetTempPath(), $"radiant-test-{Guid.NewGuid():N}");
        InstanceRegistry.RootDir = testRoot;
        try
        {
            const string Tab = "renderer-1";
            TabOwnership.WriteOwner(Tab, Secondary);
            Assert.AreEqual(Secondary, TabOwnership.ReadOwner(Tab));

            TabOwnership.ClearOwner(Tab);
            Assert.IsNull(TabOwnership.ReadOwner(Tab));
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
    public void OwnerAssignedWithinIsTrueRightAfterWriteAndFalseForTinyWindow()
    {
        var savedRoot = InstanceRegistry.RootDir;
        var testRoot = Path.Combine(Path.GetTempPath(), $"radiant-test-{Guid.NewGuid():N}");
        InstanceRegistry.RootDir = testRoot;
        try
        {
            const string Tab = "renderer-fresh";
            TabOwnership.WriteOwner(Tab, Secondary);
            // Just written → within a generous grace window (the post-tear-off startup grace).
            Assert.IsTrue(TabOwnership.OwnerAssignedWithin(Tab, TimeSpan.FromSeconds(8)));
            // A zero/negative window can never contain a past write → false (the liveness check then decides).
            Assert.IsFalse(TabOwnership.OwnerAssignedWithin(Tab, TimeSpan.Zero));
        }
        finally
        {
            InstanceRegistry.RootDir = savedRoot;
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public void OwnerAssignedWithinIsFalseWhenMarkerAbsent()
    {
        var savedRoot = InstanceRegistry.RootDir;
        var testRoot = Path.Combine(Path.GetTempPath(), $"radiant-test-{Guid.NewGuid():N}");
        InstanceRegistry.RootDir = testRoot;
        try
        {
            Assert.IsFalse(TabOwnership.OwnerAssignedWithin("renderer-absent", TimeSpan.FromSeconds(8)));
        }
        finally
        {
            InstanceRegistry.RootDir = savedRoot;
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public void ClearOwnerIsNoOpWhenMarkerAbsent()
    {
        var savedRoot = InstanceRegistry.RootDir;
        var testRoot = Path.Combine(Path.GetTempPath(), $"radiant-test-{Guid.NewGuid():N}");
        InstanceRegistry.RootDir = testRoot;
        try
        {
            // No marker written — clearing must not throw.
            TabOwnership.ClearOwner("renderer-absent");
            Assert.IsNull(TabOwnership.ReadOwner("renderer-absent"));
        }
        finally
        {
            InstanceRegistry.RootDir = savedRoot;
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }
}
