using System.Globalization;

namespace Radiant.Host.Tests;

[TestClass]
[DoNotParallelize]
public sealed class HostLockTests
{
    private string _lockPath = null!;

    [TestInitialize]
    public void Setup()
    {
        _lockPath = Path.Combine(Path.GetTempPath(), $"radiant-test-{Guid.NewGuid():N}", "host.lock");
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
    public void SpawnsHostAndRecordsPidWhenNoneRunning()
    {
        var spawns = 0;
        HostLock.EnsureSingleHost(_lockPath, hostExists: () => false, isAlive: Never, spawn: () => { spawns++; return 4242; });

        Assert.AreEqual(1, spawns);
        Assert.AreEqual(4242, ReadLockPid());
    }

    [TestMethod]
    public void DoesNotSpawnWhenLockHoldsLivePid()
    {
        File.WriteAllText(_lockPath, "1234");
        var spawns = 0;
        HostLock.EnsureSingleHost(_lockPath, hostExists: () => false, isAlive: Always, spawn: () => { spawns++; return 9; });

        Assert.AreEqual(0, spawns);
        Assert.AreEqual(1234, ReadLockPid());
    }

    [TestMethod]
    public void RespawnsWhenLockHoldsDeadPid()
    {
        File.WriteAllText(_lockPath, "1234");
        var spawns = 0;
        HostLock.EnsureSingleHost(_lockPath, hostExists: () => false, isAlive: Never, spawn: () => { spawns++; return 5678; });

        Assert.AreEqual(1, spawns);
        Assert.AreEqual(5678, ReadLockPid());
    }

    [TestMethod]
    public void TakesFastPathAndNeverTouchesLockWhenHostRegistered()
    {
        var spawns = 0;
        HostLock.EnsureSingleHost(_lockPath, hostExists: () => true, isAlive: Never, spawn: () => { spawns++; return 1; });

        Assert.AreEqual(0, spawns);
        Assert.IsFalse(File.Exists(_lockPath));
    }

    [TestMethod]
    public void TwoSequentialLaunchesSpawnExactlyOnce()
    {
        // The first launch spawns and records its PID; the second sees that live PID and backs off
        // (modelling a real race where the host has not yet registered its "tab" capability).
        var spawnedPid = 0;
        var spawns = 0;
        var alive = new HashSet<int>();
        Func<int, bool> isAlive = alive.Contains;
        Func<int> spawn = () => { spawns++; spawnedPid = 7000 + spawns; alive.Add(spawnedPid); return spawnedPid; };

        HostLock.EnsureSingleHost(_lockPath, hostExists: () => false, isAlive: isAlive, spawn: spawn);
        HostLock.EnsureSingleHost(_lockPath, hostExists: () => false, isAlive: isAlive, spawn: spawn);

        Assert.AreEqual(1, spawns);
        Assert.AreEqual(spawnedPid, ReadLockPid());
    }
}
