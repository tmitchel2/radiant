namespace Radiant.Host.AgentControlProtocol.Tests;

[TestClass]
[DoNotParallelize]
public sealed class InstanceRegistryTests
{
    private string _testRoot = null!;
    private string _savedRoot = null!;

    [TestInitialize]
    public void Setup()
    {
        _savedRoot = InstanceRegistry.RootDir;
        _testRoot = Path.Combine(Path.GetTempPath(), $"radiant-test-{Guid.NewGuid():N}");
        InstanceRegistry.RootDir = _testRoot;
    }

    [TestCleanup]
    public void Cleanup()
    {
        InstanceRegistry.RootDir = _savedRoot;
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    [TestMethod]
    public void RegisterCreatesDirectoryStructure()
    {
        var info = CreateTestInfo("test-instance");
        InstanceRegistry.Register(info);

        Assert.IsTrue(Directory.Exists(Path.Combine(_testRoot, "test-instance")));
        Assert.IsTrue(Directory.Exists(Path.Combine(_testRoot, "test-instance", "commands")));
        Assert.IsTrue(Directory.Exists(Path.Combine(_testRoot, "test-instance", "responses")));
        Assert.IsTrue(File.Exists(Path.Combine(_testRoot, "test-instance", "instance.json")));
    }

    [TestMethod]
    public void ListInstancesReturnsLiveInstances()
    {
        var info = CreateTestInfo("live-instance");
        info.Pid = Environment.ProcessId; // Current process is alive
        InstanceRegistry.Register(info);

        var instances = InstanceRegistry.ListInstances();
        Assert.AreEqual(1, instances.Length);
        Assert.AreEqual("live-instance", instances[0].Name);
    }

    [TestMethod]
    public void ListInstancesCleansUpDeadInstances()
    {
        var info = CreateTestInfo("dead-instance");
        info.Pid = 99999999; // Almost certainly not a real PID
        InstanceRegistry.Register(info);

        var instances = InstanceRegistry.ListInstances();
        Assert.AreEqual(0, instances.Length);
        Assert.IsFalse(Directory.Exists(Path.Combine(_testRoot, "dead-instance")));
    }

    [TestMethod]
    public void DeregisterRemovesDirectory()
    {
        var info = CreateTestInfo("to-remove");
        InstanceRegistry.Register(info);

        InstanceRegistry.Deregister("to-remove");
        Assert.IsFalse(Directory.Exists(Path.Combine(_testRoot, "to-remove")));
    }

    [TestMethod]
    public void GetInstanceReturnsInfoForLiveProcess()
    {
        var info = CreateTestInfo("get-test");
        info.Pid = Environment.ProcessId;
        InstanceRegistry.Register(info);

        var result = InstanceRegistry.GetInstance("get-test");
        Assert.IsNotNull(result);
        Assert.AreEqual("get-test", result.Name);
    }

    [TestMethod]
    public void GetInstanceReturnsNullForNonexistent()
    {
        var result = InstanceRegistry.GetInstance("nonexistent");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GenerateNameIsThePrefixAndThePid()
    {
        var name = InstanceRegistry.GenerateName("myapp");
        Assert.IsTrue(name.StartsWith("myapp-", StringComparison.Ordinal));
        Assert.IsTrue(name.Contains(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    [TestMethod]
    public void ListInstancesReturnsEmptyWhenNoRoot()
    {
        InstanceRegistry.RootDir = Path.Combine(_testRoot, "nonexistent");
        var instances = InstanceRegistry.ListInstances();
        Assert.AreEqual(0, instances.Length);
    }

    private static InstanceInfo CreateTestInfo(string name) => new()
    {
        Name = name,
        Pid = Environment.ProcessId,
        StartTime = DateTime.UtcNow.ToString("o"),
        WorkingDirectory = Directory.GetCurrentDirectory(),
        StateDirectory = "",
        Capabilities = ["wing", "camera"],
    };
}
