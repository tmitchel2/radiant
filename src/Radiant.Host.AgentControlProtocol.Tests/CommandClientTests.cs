using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CommandClientTests
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
    public void SendWritesCommandFile()
    {
        var info = new InstanceInfo { Name = "client-test", Pid = Environment.ProcessId };
        InstanceRegistry.Register(info);

        // Simulate the app-side: poll for commands in a background thread
        var commandsDir = InstanceRegistry.GetCommandsDir("client-test");
        var responsesDir = InstanceRegistry.GetResponsesDir("client-test");
        using var cts = new CancellationTokenSource();

        var appThread = new Thread(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                foreach (var file in Directory.GetFiles(commandsDir, "cmd-*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var cmd = JsonSerializer.Deserialize(json, AgentJsonContext.Default.AgentCommand);
                        if (cmd != null)
                        {
                            var response = AgentResponse.Ok(cmd.Id, JsonDocument.Parse($$$"""{"echo":"{{{cmd.Action}}}"}""").RootElement, 0.5);
                            var responseJson = JsonSerializer.Serialize(response, AgentJsonContext.Default.AgentResponse);
                            var responsePath = Path.Combine(responsesDir, $"cmd-{cmd.Id}.json");
                            File.WriteAllText(responsePath, responseJson);
                            File.Delete(file);
                        }
                    }
#pragma warning disable CA1031 // Test polling simulation
                    catch { /* retry next poll */ }
#pragma warning restore CA1031
                }
                Thread.Sleep(20);
            }
        });
        appThread.Start();

        try
        {
            var client = new CommandClient("client-test");
            var result = client.Send("scene.active", timeoutMs: 5000);

            Assert.AreEqual("ok", result.Status);
            Assert.IsNotNull(result.Result);
        }
        finally
        {
            cts.Cancel();
            appThread.Join(1000);
        }
    }

    [TestMethod]
    public void SendTimesOutWhenNoResponse()
    {
        var info = new InstanceInfo { Name = "timeout-test", Pid = Environment.ProcessId };
        InstanceRegistry.Register(info);

        var client = new CommandClient("timeout-test");
        var result = client.Send("nonexistent", timeoutMs: 500);

        Assert.AreEqual("error", result.Status);
        Assert.AreEqual("timeout", result.Error?.Code);
    }
}
