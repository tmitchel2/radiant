using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CommandReceiverTests
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
    public void DrainsPendingCommands()
    {
        var info = new InstanceInfo { Name = "queue-test", Pid = Environment.ProcessId };
        InstanceRegistry.Register(info);

        using var queue = new CommandReceiver("queue-test");

        // Write a command file
        var cmd = new AgentCommand
        {
            Id = "test-001",
            Action = "scene.active",
            Timestamp = DateTime.UtcNow.ToString("o"),
        };
        var json = JsonSerializer.Serialize(cmd, AgentJsonContext.Default.AgentCommand);
        var cmdPath = Path.Combine(InstanceRegistry.GetCommandsDir("queue-test"), "cmd-test-001.json");
        File.WriteAllText(cmdPath, json);

        // Allow FileSystemWatcher to detect the file
        Thread.Sleep(200);

        var commands = queue.DrainPendingCommands();
        Assert.AreEqual(1, commands.Count);
        Assert.AreEqual("test-001", commands[0].Id);
        Assert.AreEqual("scene.active", commands[0].Action);

        // Command file should be deleted after drain
        Assert.IsFalse(File.Exists(cmdPath));
    }

    [TestMethod]
    public void WritesResponseFile()
    {
        var info = new InstanceInfo { Name = "response-test", Pid = Environment.ProcessId };
        InstanceRegistry.Register(info);

        using var queue = new CommandReceiver("response-test");

        var response = AgentResponse.Ok("test-002", JsonDocument.Parse("""{"path":"scenes/test.csx"}""").RootElement, 1.5);
        queue.WriteResponse(response);

        var responsePath = Path.Combine(InstanceRegistry.GetResponsesDir("response-test"), "cmd-test-002.json");
        Assert.IsTrue(File.Exists(responsePath));

        var json = File.ReadAllText(responsePath);
        Assert.IsTrue(json.Contains("test-002", StringComparison.Ordinal));
        Assert.IsTrue(json.Contains("ok", StringComparison.Ordinal));
    }

    [TestMethod]
    public void WriteResponseDoesNotThrowOnFailedWrite()
    {
        var info = new InstanceInfo { Name = "resilience-test", Pid = Environment.ProcessId };
        InstanceRegistry.Register(info);

        using var queue = new CommandReceiver("resilience-test");

        // Simulate a transient FS failure / race (e.g. two processes briefly sharing an instance dir, a
        // lost File.Move): removing the responses dir makes the atomic write fail. WriteResponse must
        // swallow it and not crash the caller's render loop.
        Directory.Delete(InstanceRegistry.GetResponsesDir("resilience-test"), recursive: true);

        var response = AgentResponse.Ok("r-1", null, 1.0);
        queue.WriteResponse(response); // must not throw

        var responsePath = Path.Combine(InstanceRegistry.GetResponsesDir("resilience-test"), "cmd-r-1.json");
        Assert.IsFalse(File.Exists(responsePath));
    }

    [TestMethod]
    public void WaitForResponsesDeliveredReturnsWhenDrained()
    {
        var info = new InstanceInfo { Name = "drain-test", Pid = Environment.ProcessId };
        InstanceRegistry.Register(info);

        using var queue = new CommandReceiver("drain-test");

        // No pending responses → returns effectively immediately (well under the timeout).
        var sw = System.Diagnostics.Stopwatch.StartNew();
        queue.WaitForResponsesDelivered(2000);
        sw.Stop();
        Assert.IsTrue(sw.ElapsedMilliseconds < 500, $"should return promptly when drained, took {sw.ElapsedMilliseconds}ms");

        // A written-but-unread response keeps it waiting until the timeout (client never read it).
        queue.WriteResponse(AgentResponse.Ok("pending-1", null, 1.0));
        sw.Restart();
        queue.WaitForResponsesDelivered(300);
        sw.Stop();
        Assert.IsTrue(sw.ElapsedMilliseconds >= 250, $"should wait out the timeout while undrained, took {sw.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public void EmptyQueueReturnsEmptyList()
    {
        var info = new InstanceInfo { Name = "empty-test", Pid = Environment.ProcessId };
        InstanceRegistry.Register(info);

        using var queue = new CommandReceiver("empty-test");
        var commands = queue.DrainPendingCommands();
        Assert.AreEqual(0, commands.Count);
    }
}
