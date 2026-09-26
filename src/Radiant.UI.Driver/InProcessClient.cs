using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;
using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Automation;
using Radiant.UI.Core;

namespace Radiant.UI.Driver;

/// <summary>
/// An <see cref="IAgentClient"/> for an app in this process, stepped on the fixed clock: each command is
/// queued on the automation's dispatcher and frames are run, right here on the calling thread, until it's
/// answered. The same actions run as over a transport, so a test means the same in-process as out.
/// </summary>
public sealed class InProcessClient : IAgentClient
{
    private readonly UIAutomation _automation;
    private readonly Connection _connection = new();
    private readonly bool _owns;
    // One command at a time: the app runs on whichever thread sends, and two can't run its frames at once.
    private readonly Lock _gate = new();
    private int _nextId;

    /// <summary>A client for <paramref name="automation"/>'s app; disposing it disposes the app if <paramref name="owns"/>.</summary>
    public InProcessClient(UIAutomation automation, bool owns = false)
    {
        ArgumentNullException.ThrowIfNull(automation);
        _automation = automation;
        _owns = owns;
        Hello = new AgentHello
        {
            Instance = automation.InstanceName ?? "in-process",
            App = automation.AppName,
            Capabilities = ["ui", "ui.screenshot", "log", "clock.fixed"],
            Clock = automation.Session.ClockMode.ToString().ToLowerInvariant(),
            Headless = automation.Session.IsHeadless,
        };
    }

    /// <summary>The app.</summary>
    public UIAppSession Session => _automation.Session;

    /// <summary>Its automation.</summary>
    public UIAutomation Automation => _automation;

    /// <inheritdoc/>
    public string Instance => Hello.Instance;

    /// <inheritdoc/>
    public AgentHello Hello { get; }

    /// <inheritdoc/>
    public string Transport => "in-process";

    /// <inheritdoc/>
    public ChannelReader<AgentEvent>? Events => _connection.Events.Reader;

    /// <summary>The most frames one command may run, whatever its timeout: a guard against a wait that never ends.</summary>
    public int MaxFramesPerCommand { get; set; } = 60 * 120;

    /// <inheritdoc/>
    public Task<AgentResponse> SendAsync(string action, JsonElement? parameters = null, int? timeoutMs = null, CancellationToken cancellation = default)
    {
        lock (_gate)
        {
            return Task.FromResult(Send(action, parameters, timeoutMs, cancellation));
        }
    }

    private AgentResponse Send(string action, JsonElement? parameters, int? timeoutMs, CancellationToken cancellation)
    {
        var id = "t" + (++_nextId).ToString(CultureInfo.InvariantCulture);
        _connection.Pending = null;
        _automation.Dispatcher.Enqueue(new AgentCommand
        {
            Id = id,
            Action = action,
            Params = parameters,
            TimeoutMs = timeoutMs,
            Timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
        }, _connection);
        for (var frames = 0; _connection.Pending is null; frames++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (frames >= MaxFramesPerCommand)
            {
                _automation.Dispatcher.Cancel(_connection, id);
                _automation.Session.Step();
                return _connection.Pending ?? AgentResponse.Err(id, AgentErrorCodes.Timeout, $"{action} ran {frames} frames without answering.");
            }
            _automation.Session.Step();
        }
        return _connection.Pending;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        using var held = _gate.EnterScope();
        _connection.Events.Writer.TryComplete();
        if (_owns)
        {
            _automation.Dispose();
            _automation.Session.Dispose();
        }
        return ValueTask.CompletedTask;
    }

    private sealed class Connection : IAgentConnection
    {
        public AgentResponse? Pending { get; set; }

        public Channel<AgentEvent> Events { get; } = Channel.CreateUnbounded<AgentEvent>();

        public string Client => "in-process";

        public bool CanStream => true;

        public void Send(AgentResponse response) => Pending = response;

        public void SendEvent(AgentEvent agentEvent) => Events.Writer.TryWrite(agentEvent);
    }
}
