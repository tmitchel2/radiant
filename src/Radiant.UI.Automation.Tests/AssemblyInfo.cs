using Radiant.Host.AgentControlProtocol;

// Each test drives its own app on its own thread; they don't share state.
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Radiant.UI.Automation.Tests;

[TestClass]
public static class TestRegistry
{
    /// <summary>Where this run's apps register: short, so their sockets' paths fit, and away from any real ones.</summary>
    public static string Root { get; } = Path.Combine("/tmp", "rua-" + Guid.NewGuid().ToString("N")[..8], "instances");

    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        _ = context;
        InstanceRegistry.RootDir = Root;
    }

    [AssemblyCleanup]
    public static void Cleanup()
    {
        var run = Path.GetDirectoryName(Root)!;
        if (Directory.Exists(run))
        {
            Directory.Delete(run, recursive: true);
        }
    }
}
