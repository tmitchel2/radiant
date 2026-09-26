using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol.Tests;

[TestClass]
[DoNotParallelize]
public sealed class TransportTests
{
    private string _testRoot = null!;
    private string _savedRoot = null!;
    private AgentDispatcher _dispatcher = null!;
    private volatile bool _stopping;
    private Task _loop = null!;

    [TestInitialize]
    public void Setup()
    {
        _savedRoot = InstanceRegistry.RootDir;
        // Short, so the socket fits a Unix socket path.
        _testRoot = Path.Combine("/tmp", $"rt-{Guid.NewGuid():N}"[..11]);
        InstanceRegistry.RootDir = _testRoot;
        _dispatcher = new AgentDispatcher();
        _dispatcher.RegisterSync(new ActionDefinition { Name = "echo" }, ActionKind.Query, context => context.Params.Clone());
        _dispatcher.Register(new ActionDefinition { Name = "never" }, ActionKind.Mutation, _ => AgentOperation.FromPoll(() => null));
        _dispatcher.RegisterSync(new ActionDefinition { Name = "stream" }, ActionKind.Query, context =>
        {
            context.Connection.SendEvent(new AgentEvent { Event = "log", Sub = "s1", Seq = 1 });
            return (JsonElement?)null;
        });
        // A stand-in frame loop, as the app's UI thread would run it.
        _stopping = false;
        _loop = Task.Run(async () =>
        {
            while (!_stopping)
            {
                _dispatcher.Pump();
                await Task.Delay(5, CancellationToken.None);
            }
        }, CancellationToken.None);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _stopping = true;
        await _loop;
        InstanceRegistry.RootDir = _savedRoot;
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task TheSocketGreetsAndAnswers()
    {
        using var server = new AgentSocketServer(InstanceRegistry.GetSocketPath("app"), _dispatcher, () => new AgentHello { Instance = "app", Capabilities = ["ui"] });
        server.Start();

        await using var client = await AgentSocketClient.ConnectAsync(server.SocketPath);
        var response = await client.SendAsync("echo", JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement> { ["x"] = JsonSerializer.SerializeToElement(3, AgentJsonContext.Default.Int32) }, AgentJsonContext.Default.DictionaryStringJsonElement));

        Assert.AreEqual("app", client.Hello.Instance);
        Assert.AreEqual(AgentProtocol.Version, client.Hello.Protocol);
        Assert.AreEqual("ok", response.Status, response.Error?.Message);
        Assert.AreEqual(3, response.Result!.Value.GetProperty("x").GetInt32());
    }

    [TestMethod]
    public async Task TheSocketAnswersConcurrentCommandsById()
    {
        using var server = new AgentSocketServer(InstanceRegistry.GetSocketPath("app"), _dispatcher, () => new AgentHello());
        server.Start();
        await using var client = await AgentSocketClient.ConnectAsync(server.SocketPath);

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            client.SendAsync("echo", JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement> { ["i"] = JsonSerializer.SerializeToElement(i, AgentJsonContext.Default.Int32) }, AgentJsonContext.Default.DictionaryStringJsonElement))));

        CollectionAssert.AreEqual(Enumerable.Range(0, 20).ToList(), responses.Select(r => r.Result!.Value.GetProperty("i").GetInt32()).ToList());
    }

    [TestMethod]
    public async Task TheSocketPushesEvents()
    {
        using var server = new AgentSocketServer(InstanceRegistry.GetSocketPath("app"), _dispatcher, () => new AgentHello());
        server.Start();
        await using var client = await AgentSocketClient.ConnectAsync(server.SocketPath);

        await client.SendAsync("stream");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var agentEvent = await client.Events.ReadAsync(timeout.Token);

        Assert.AreEqual("log", agentEvent.Event);
        Assert.AreEqual("s1", agentEvent.Sub);
    }

    [TestMethod]
    public async Task TheSocketTimesOutServerSide()
    {
        using var server = new AgentSocketServer(InstanceRegistry.GetSocketPath("app"), _dispatcher, () => new AgentHello());
        server.Start();
        await using var client = await AgentSocketClient.ConnectAsync(server.SocketPath);

        var response = await client.SendAsync("never", timeoutMs: 100);

        Assert.AreEqual(AgentErrorCodes.Timeout, response.Error!.Code);
    }

    [TestMethod]
    public async Task CancellingOnTheClientCancelsOnTheServer()
    {
        var completed = new TaskCompletionSource<string>();
        _dispatcher.Completed += (_, response) => completed.TrySetResult(response.Error?.Code ?? "ok");
        using var server = new AgentSocketServer(InstanceRegistry.GetSocketPath("app"), _dispatcher, () => new AgentHello());
        server.Start();
        await using var client = await AgentSocketClient.ConnectAsync(server.SocketPath);
        using var cancel = new CancellationTokenSource(100);

        await Assert.ThrowsAsync<OperationCanceledException>(() => client.SendAsync("never", cancellation: cancel.Token));

        Assert.AreEqual(AgentErrorCodes.Cancelled, await completed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void TheSocketIsOnlyForThisUser()
    {
        using var server = new AgentSocketServer(InstanceRegistry.GetSocketPath("app"), _dispatcher, () => new AgentHello());
        server.Start();

        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(server.SocketPath));
    }

    [TestMethod]
    public void ALongSocketPathMovesToTheTempDirectory()
    {
        var name = new string('n', 120);

        var path = InstanceRegistry.GetSocketPath(name);

        Assert.IsTrue(System.Text.Encoding.UTF8.GetByteCount(path) <= 104, path);
        Assert.AreEqual(path, InstanceRegistry.GetSocketPath(name));
    }

    [TestMethod]
    public void FilesRunThroughTheDispatcher()
    {
        InstanceRegistry.Register(new InstanceInfo { Name = "files", Pid = Environment.ProcessId });
        using var transport = new FileDropTransport("files", _dispatcher);

        var response = new CommandClient("files").Send("echo", """{"y":"yes"}""", timeoutMs: 5000);

        Assert.AreEqual("ok", response.Status, response.Error?.Message);
        Assert.AreEqual("yes", response.Result!.Value.GetProperty("y").GetString());
        Assert.IsNull(response.Type);
    }
}
