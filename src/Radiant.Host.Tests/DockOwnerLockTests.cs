using System.Globalization;

namespace Radiant.Host.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DockOwnerLockTests
{
    private string _lockPath = null!;

    [TestInitialize]
    public void Setup()
    {
        _lockPath = Path.Combine(Path.GetTempPath(), $"radiant-test-{Guid.NewGuid():N}", "dock-owner.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(_lockPath)!);
    }

    [TestCleanup]
    public void Cleanup()
    {
        var dir = Path.GetDirectoryName(_lockPath)!;
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    private static bool Never(int _) => false;
    private static bool Always(int _) => true;

    private int ReadLockPid() => int.Parse(File.ReadAllText(_lockPath).Trim(), CultureInfo.InvariantCulture);

    [TestMethod]
    public void ClaimsOwnershipWhenNoneRecorded()
    {
        var owned = DockOwnerLock.TryAcquire(_lockPath, Never);

        Assert.IsTrue(owned);
        Assert.AreEqual(Environment.ProcessId, ReadLockPid());
    }

    [TestMethod]
    public void DoesNotClaimWhenLiveOwnerRecorded()
    {
        File.WriteAllText(_lockPath, "1234");

        var owned = DockOwnerLock.TryAcquire(_lockPath, Always);

        Assert.IsFalse(owned);
        Assert.AreEqual(1234, ReadLockPid(), "a live owner's PID must be left untouched");
    }

    [TestMethod]
    public void ReclaimsWhenRecordedOwnerIsDead()
    {
        File.WriteAllText(_lockPath, "1234");

        var owned = DockOwnerLock.TryAcquire(_lockPath, Never);

        Assert.IsTrue(owned);
        Assert.AreEqual(Environment.ProcessId, ReadLockPid());
    }

    [TestMethod]
    public void ReturnsTrueWhenAlreadyOwnedBySelf()
    {
        File.WriteAllText(_lockPath, Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

        // isAlive=Always would normally back off, but the recorded PID is ours, so we still own it.
        var owned = DockOwnerLock.TryAcquire(_lockPath, Always);

        Assert.IsTrue(owned);
        Assert.AreEqual(Environment.ProcessId, ReadLockPid());
    }
}
