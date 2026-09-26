using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol.Tests;

[TestClass]
public sealed class AgentDispatcherTests
{
    private double _now;
    private AgentDispatcher _dispatcher = null!;
    private RecordingConnection _connection = null!;

    [TestInitialize]
    public void Setup()
    {
        _now = 0;
        _dispatcher = new AgentDispatcher(() => _now);
        _connection = new RecordingConnection();
    }

    [TestMethod]
    public void AnswersAnUnknownActionNotFound()
    {
        Send("nope");

        _dispatcher.Pump();

        Assert.AreEqual(AgentErrorCodes.NotFound, _connection.Single("1").Error!.Code);
    }

    [TestMethod]
    public void ListsItsActions()
    {
        _dispatcher.RegisterSync(new ActionDefinition { Name = "ui.tap" }, ActionKind.Mutation, _ => (JsonElement?)null);
        Send("actions.list");

        _dispatcher.Pump();

        var names = _connection.Single("1").Result!.Value.EnumerateArray().Select(a => a.GetProperty("name").GetString()).ToList();
        CollectionAssert.AreEqual(new[] { "actions.list", "ui.tap" }, names);
    }

    [TestMethod]
    public void RunsMutationsOneAtATimeInOrder()
    {
        var order = new List<string>();
        var released = false;
        _dispatcher.Register(new ActionDefinition { Name = "slow" }, ActionKind.Mutation, _ =>
        {
            order.Add("slow started");
            return AgentOperation.FromPoll(() => released ? Ok() : null);
        });
        _dispatcher.Register(new ActionDefinition { Name = "fast" }, ActionKind.Mutation, _ =>
        {
            order.Add("fast started");
            return AgentOperation.Done(Ok());
        });
        Send("slow", "a");
        Send("fast", "b");

        _dispatcher.Pump();
        _dispatcher.Pump();
        Assert.AreEqual(0, _connection.Responses.Count);
        CollectionAssert.AreEqual(new[] { "slow started" }, order);

        released = true;
        _dispatcher.Pump();

        CollectionAssert.AreEqual(new[] { "a", "b" }, _connection.Responses.Select(r => r.Id).ToList());
        CollectionAssert.AreEqual(new[] { "slow started", "fast started" }, order);
        Assert.IsFalse(_dispatcher.HasPending);
    }

    [TestMethod]
    public void QueriesDoNotWaitForMutations()
    {
        _dispatcher.Register(new ActionDefinition { Name = "slow" }, ActionKind.Mutation, _ => AgentOperation.FromPoll(() => null));
        _dispatcher.RegisterSync(new ActionDefinition { Name = "read" }, ActionKind.Query, _ => JsonSerializer.SerializeToElement(true, AgentJsonContext.Default.Boolean));
        Send("slow", "a");
        Send("read", "b");

        _dispatcher.Pump();

        Assert.AreEqual("b", _connection.Responses.Single().Id);
        Assert.IsTrue(_dispatcher.HasPending);
    }

    [TestMethod]
    public void TimesOutOnItsClockWithTheOperationsReason()
    {
        var cancelled = false;
        _dispatcher.Register(new ActionDefinition { Name = "wait" }, ActionKind.Mutation, _ => new NeverOperation(() => cancelled = true));
        Send("wait", "a", timeoutMs: 500);

        _dispatcher.Pump();
        _now = 0.4;
        _dispatcher.Pump();
        Assert.AreEqual(0, _connection.Responses.Count);
        _now = 0.5;
        _dispatcher.Pump();

        var error = _connection.Single("a").Error!;
        Assert.AreEqual(AgentErrorCodes.Busy, error.Code);
        Assert.IsTrue(cancelled);
    }

    [TestMethod]
    public void QueuedMutationsTimeOutToo()
    {
        _dispatcher.Register(new ActionDefinition { Name = "wait" }, ActionKind.Mutation, _ => AgentOperation.FromPoll(() => null));
        Send("wait", "a", timeoutMs: 10_000);
        Send("wait", "b", timeoutMs: 100);

        _dispatcher.Pump();
        _now = 0.2;
        _dispatcher.Pump();

        Assert.AreEqual(AgentErrorCodes.Timeout, _connection.Single("b").Error!.Code);
        Assert.AreEqual(1, _connection.Responses.Count);
    }

    [TestMethod]
    public void CancelsOnRequest()
    {
        var cancelled = false;
        _dispatcher.Register(new ActionDefinition { Name = "wait" }, ActionKind.Mutation, _ => new NeverOperation(() => cancelled = true));
        Send("wait", "a");
        _dispatcher.Pump();

        _dispatcher.Cancel(_connection, "a");
        _dispatcher.Pump();

        Assert.AreEqual(AgentErrorCodes.Cancelled, _connection.Single("a").Error!.Code);
        Assert.IsTrue(cancelled);
        Assert.IsFalse(_dispatcher.HasPending);
    }

    [TestMethod]
    public void DropsAClosedConnectionsCommandsSilently()
    {
        var completed = new List<string>();
        _dispatcher.Completed += (context, response) => completed.Add(response.Error?.Code ?? "ok");
        _dispatcher.Register(new ActionDefinition { Name = "wait" }, ActionKind.Mutation, _ => AgentOperation.FromPoll(() => null));
        Send("wait", "a");
        Send("wait", "b");
        _dispatcher.Pump();

        _dispatcher.CancelConnection(_connection);
        _dispatcher.Pump();

        Assert.AreEqual(0, _connection.Responses.Count);
        CollectionAssert.AreEqual(new[] { AgentErrorCodes.Cancelled, AgentErrorCodes.Cancelled }, completed);
        Assert.IsFalse(_dispatcher.HasPending);
    }

    [TestMethod]
    public void MapsExceptionsToErrorCodes()
    {
        _dispatcher.RegisterSync(new ActionDefinition { Name = "agent" }, ActionKind.Query, _ => throw new AgentException(AgentErrorCodes.NoMatch, "none"));
        _dispatcher.RegisterSync(new ActionDefinition { Name = "args" }, ActionKind.Query, _ => throw new ArgumentException("bad"));
        _dispatcher.RegisterSync(new ActionDefinition { Name = "boom" }, ActionKind.Query, _ => throw new InvalidOperationException("boom"));
        Send("agent", "a");
        Send("args", "b");
        Send("boom", "c");

        _dispatcher.Pump();

        Assert.AreEqual(AgentErrorCodes.NoMatch, _connection.Single("a").Error!.Code);
        Assert.AreEqual(AgentErrorCodes.InvalidParams, _connection.Single("b").Error!.Code);
        Assert.AreEqual(AgentErrorCodes.Internal, _connection.Single("c").Error!.Code);
    }

    [TestMethod]
    public void StampsResponsesAndReportsStartAndFinish()
    {
        var events = new List<string>();
        _dispatcher.FrameSource = () => 42;
        _dispatcher.Started += context => events.Add("start " + context.Command.Id);
        _dispatcher.Completed += (context, response) => events.Add("done " + response.Id);
        _dispatcher.RegisterSync(new ActionDefinition { Name = "go" }, ActionKind.Query, _ => (JsonElement?)null);
        Send("go", "a");

        _dispatcher.Pump();

        var response = _connection.Single("a");
        Assert.AreEqual(42, response.Frame);
        Assert.AreNotEqual("", response.Timestamp);
        CollectionAssert.AreEqual(new[] { "start a", "done a" }, events);
    }

    [TestMethod]
    public void WakesTheLoopWhenWorkArrives()
    {
        var woken = 0;
        _dispatcher.WorkArrived += () => woken++;

        Send("anything");

        Assert.AreEqual(1, woken);
        Assert.IsTrue(_dispatcher.HasPending);
    }

    private static AgentResponse Ok() => AgentResponse.Ok("", null, 0);

    private void Send(string action, string id = "1", int? timeoutMs = null) =>
        _dispatcher.Enqueue(new AgentCommand { Id = id, Action = action, TimeoutMs = timeoutMs }, _connection);

    private sealed class NeverOperation(Action onCancel) : AgentOperation
    {
        public override AgentResponse? Poll() => null;

        public override AgentResponse OnTimeout(AgentCallContext context) => AgentResponse.Err("", AgentErrorCodes.Busy, "still animating");

        public override void OnCancel() => onCancel();
    }
}

internal sealed class RecordingConnection : IAgentConnection
{
    public List<AgentResponse> Responses { get; } = [];

    public List<AgentEvent> Events { get; } = [];

    public string Client => "test";

    public bool CanStream => true;

    public void Send(AgentResponse response) => Responses.Add(response);

    public void SendEvent(AgentEvent agentEvent) => Events.Add(agentEvent);

    public AgentResponse Single(string id) => Responses.Single(r => r.Id == id);
}
