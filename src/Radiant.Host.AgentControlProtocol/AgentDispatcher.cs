using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>How an action is scheduled.</summary>
public enum ActionKind
{
    /// <summary>Reads state: starts at the next frame, alongside anything else.</summary>
    Query,

    /// <summary>Changes state: runs after every mutation sent before it has finished, one at a time.</summary>
    Mutation,
}

/// <summary>Where a command came from, and where its response goes.</summary>
public interface IAgentConnection
{
    /// <summary>Who's on the other end, for the log: <c>file</c>, <c>socket#3</c>, <c>in-process</c>.</summary>
    string Client { get; }

    /// <summary>True if <see cref="SendEvent"/> reaches anyone.</summary>
    bool CanStream { get; }

    /// <summary>Delivers a response. Called on the dispatcher's thread; must not block.</summary>
    void Send(AgentResponse response);

    /// <summary>Pushes an event. Called on the dispatcher's thread; must not block.</summary>
    void SendEvent(AgentEvent agentEvent);
}

/// <summary>A command being run: what it is, who sent it, and how long it has.</summary>
public sealed class AgentCallContext
{
    internal AgentCallContext(AgentDispatcher dispatcher, AgentCommand command, IAgentConnection connection, double deadline)
    {
        Dispatcher = dispatcher;
        Command = command;
        Connection = connection;
        Deadline = deadline;
    }

    /// <summary>The dispatcher running it.</summary>
    public AgentDispatcher Dispatcher { get; }

    /// <summary>The command.</summary>
    public AgentCommand Command { get; }

    /// <summary>Its params, or an empty object.</summary>
    public JsonElement Params => Command.Params is { ValueKind: JsonValueKind.Object } p ? p : s_empty;

    /// <summary>Where it came from.</summary>
    public IAgentConnection Connection { get; }

    /// <summary>When, on <see cref="AgentDispatcher.Now"/>, it times out.</summary>
    public double Deadline { get; }

    /// <summary>When, on <see cref="AgentDispatcher.Now"/>, it started.</summary>
    public double StartTime { get; internal set; }

    /// <summary>The frame it started on.</summary>
    public long StartFrame { get; internal set; }

    internal Stopwatch Stopwatch { get; } = new();

    internal AgentOperation? Operation { get; set; }

    internal bool Started { get; set; }

    internal ActionKind Kind { get; set; }

    private static readonly JsonElement s_empty = JsonDocument.Parse("{}").RootElement.Clone();
}

/// <summary>
/// A command in progress. The dispatcher polls it once a frame on the UI thread until it answers, so an
/// action that waits (for idle, for an element) spans frames without blocking them.
/// </summary>
public abstract class AgentOperation
{
    /// <summary>
    /// Advances the command a frame: its response when it's done (the dispatcher fills in the id and
    /// timing), or null while it's still going. Throwing <see cref="AgentException"/> answers its error.
    /// </summary>
    public abstract AgentResponse? Poll();

    /// <summary>The response when it times out; override to say what it was waiting for.</summary>
    public virtual AgentResponse OnTimeout(AgentCallContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return AgentResponse.Err("", AgentErrorCodes.Timeout,
            $"{context.Command.Action} didn't finish within {(context.Deadline - context.StartTime) * 1000:0}ms.");
    }

    /// <summary>Called when it's cancelled, to let go of anything it holds.</summary>
    public virtual void OnCancel()
    {
    }

    /// <summary>An operation already done.</summary>
    public static AgentOperation Done(AgentResponse response) => new PollOperation(() => response);

    /// <summary>An operation already done, with <paramref name="result"/>.</summary>
    public static AgentOperation Done(JsonElement? result) => Done(AgentResponse.Ok("", result, 0));

    /// <summary>An operation that polls <paramref name="poll"/>, with an optional timeout response.</summary>
    public static AgentOperation FromPoll(Func<AgentResponse?> poll, Func<AgentCallContext, AgentResponse>? onTimeout = null) =>
        new PollOperation(poll, onTimeout);

    private sealed class PollOperation(Func<AgentResponse?> poll, Func<AgentCallContext, AgentResponse>? onTimeout = null) : AgentOperation
    {
        public override AgentResponse? Poll() => poll();

        public override AgentResponse OnTimeout(AgentCallContext context) => onTimeout?.Invoke(context) ?? base.OnTimeout(context);
    }
}

/// <summary>An action's failure, with its <see cref="AgentErrorCodes">code</see>; the dispatcher answers it as an error.</summary>
public sealed class AgentException : Exception
{
    /// <summary>A failure with a code, a message and what's known about it.</summary>
    public AgentException(string code, string message, JsonElement? details = null) : base(message)
    {
        Code = code;
        Details = details;
    }

    /// <summary>A failure.</summary>
    public AgentException() : this(AgentErrorCodes.Internal, "The action failed.")
    {
    }

    /// <summary>A failure with a message.</summary>
    public AgentException(string message) : this(AgentErrorCodes.Internal, message)
    {
    }

    /// <summary>A failure with a message and a cause.</summary>
    public AgentException(string message, Exception innerException) : base(message, innerException)
    {
        Code = AgentErrorCodes.Internal;
    }

    /// <summary>The error code.</summary>
    public string Code { get; } = AgentErrorCodes.Internal;

    /// <summary>What's known about it.</summary>
    public JsonElement? Details { get; }
}

/// <summary>
/// Runs agent commands from any number of transports against one application. Transports call
/// <see cref="Enqueue"/> from any thread; the application calls <see cref="Pump"/> once a frame on its UI
/// thread, where every action runs. Queries start at the next frame; mutations run one at a time in the
/// order they arrived. Every command is answered exactly once: its result, its error, <c>timeout</c> or
/// <c>cancelled</c>.
/// </summary>
public sealed class AgentDispatcher
{
    private readonly Dictionary<string, Registration> _actions = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<AgentCallContext> _incoming = new();
    private readonly ConcurrentQueue<(IAgentConnection Connection, string? Id)> _cancels = new();
    private readonly List<AgentCallContext> _queries = [];
    private readonly LinkedList<AgentCallContext> _mutations = new();
    private readonly Func<double> _clock;

    /// <summary>
    /// A dispatcher timing commands by <paramref name="clock"/>, in seconds: the wall clock by default; a
    /// fixed-step app passes its virtual clock, so waits and timeouts count in frames it has run.
    /// </summary>
    public AgentDispatcher(Func<double>? clock = null)
    {
        if (clock is null)
        {
            var stopwatch = Stopwatch.StartNew();
            clock = () => stopwatch.Elapsed.TotalSeconds;
        }
        _clock = clock;
        RegisterSync(new ActionDefinition { Name = "actions.list", Category = "actions", Description = "The actions this instance answers." },
            ActionKind.Query,
            _ => JsonSerializer.SerializeToElement(Actions.ToArray(), AgentJsonContext.Default.ActionDefinitionArray));
    }

    /// <summary>Raised on the enqueuing thread when a command arrives, to wake an idle frame loop.</summary>
    public event Action? WorkArrived;

    /// <summary>Raised on the UI thread when a command starts.</summary>
    public event Action<AgentCallContext>? Started;

    /// <summary>Raised on the UI thread when a command is answered, with its response.</summary>
    public event Action<AgentCallContext, AgentResponse>? Completed;

    /// <summary>The current frame, stamped on responses; null if the app doesn't count them.</summary>
    public Func<long>? FrameSource { get; set; }

    /// <summary>How long a command may take when it doesn't say.</summary>
    public int DefaultTimeoutMs { get; set; } = AgentProtocol.DefaultTimeoutMs;

    /// <summary>The time on the dispatcher's clock, in seconds.</summary>
    public double Now => _clock();

    /// <summary>The current frame, or 0.</summary>
    public long Frame => FrameSource?.Invoke() ?? 0;

    /// <summary>The registered actions.</summary>
    public IEnumerable<ActionDefinition> Actions => _actions.Values.Select(r => r.Definition).OrderBy(d => d.Name, StringComparer.Ordinal);

    /// <summary>True while a command waits to start or to finish: the frame loop should keep running.</summary>
    public bool HasPending => !_incoming.IsEmpty || !_cancels.IsEmpty || _queries.Count > 0 || _mutations.Count > 0;

    /// <summary>Registers an action; a later registration of the same name replaces it.</summary>
    public void Register(ActionDefinition definition, ActionKind kind, Func<AgentCallContext, AgentOperation> start)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(start);
        _actions[definition.Name] = new Registration(definition, kind, start);
    }

    /// <summary>Registers an action that answers at once with a result (or null).</summary>
    public void RegisterSync(ActionDefinition definition, ActionKind kind, Func<AgentCallContext, JsonElement?> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Register(definition, kind, context => AgentOperation.Done(handler(context)));
    }

    /// <summary>True if an action has this name.</summary>
    public bool Handles(string action) => _actions.ContainsKey(action);

    /// <summary>Queues a command from <paramref name="connection"/>. Any thread.</summary>
    public void Enqueue(AgentCommand command, IAgentConnection connection)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(connection);
        _incoming.Enqueue(new AgentCallContext(this, command, connection, double.NaN));
        WorkArrived?.Invoke();
    }

    /// <summary>Cancels the command <paramref name="id"/> from <paramref name="connection"/>. Any thread.</summary>
    public void Cancel(IAgentConnection connection, string id)
    {
        _cancels.Enqueue((connection, id));
        WorkArrived?.Invoke();
    }

    /// <summary>Cancels everything from a connection that has gone, without answering it. Any thread.</summary>
    public void CancelConnection(IAgentConnection connection)
    {
        _cancels.Enqueue((connection, null));
        WorkArrived?.Invoke();
    }

    /// <summary>Starts, advances and answers commands. Once a frame, on the UI thread.</summary>
    public void Pump()
    {
        var now = Now;
        while (_incoming.TryDequeue(out var queued))
        {
            if (!_actions.TryGetValue(queued.Command.Action, out var registration))
            {
                Answer(queued, AgentResponse.Err("", AgentErrorCodes.NotFound, $"No action '{queued.Command.Action}'. Send actions.list for the list."));
                continue;
            }
            var timeout = queued.Command.TimeoutMs is > 0 and var ms ? ms : DefaultTimeoutMs;
            var context = new AgentCallContext(this, queued.Command, queued.Connection, now + timeout / 1000.0) { Kind = registration.Kind };
            context.Stopwatch.Start();
            if (registration.Kind == ActionKind.Mutation)
            {
                _mutations.AddLast(context);
            }
            else
            {
                _queries.Add(context);
            }
        }
        while (_cancels.TryDequeue(out var cancel))
        {
            CancelMatching(cancel.Connection, cancel.Id);
        }
        for (var i = 0; i < _queries.Count; i++)
        {
            if (Step(_queries[i]))
            {
                _queries.RemoveAt(i--);
            }
        }
        // A mutation that finishes at once lets the next one start in the same frame.
        while (_mutations.First is { } head && Step(head.Value))
        {
            _mutations.RemoveFirst();
        }
        // Mutations waiting their turn still time out.
        for (var node = _mutations.First?.Next; node is not null;)
        {
            var next = node.Next;
            if (Now >= node.Value.Deadline)
            {
                Answer(node.Value, AgentResponse.Err("", AgentErrorCodes.Timeout, $"{node.Value.Command.Action} was still queued behind other actions after {node.Value.Stopwatch.ElapsedMilliseconds}ms."));
                _mutations.Remove(node);
            }
            node = next;
        }
    }

    // Runs one frame of a command: true once it's answered.
    private bool Step(AgentCallContext context)
    {
        try
        {
            if (!context.Started)
            {
                context.Started = true;
                context.StartTime = Now;
                context.StartFrame = Frame;
                Started?.Invoke(context);
                context.Operation = _actions[context.Command.Action].Start(context);
            }
            if (context.Operation!.Poll() is { } response)
            {
                Answer(context, response);
                return true;
            }
            if (Now >= context.Deadline)
            {
                context.Operation.OnCancel();
                Answer(context, context.Operation.OnTimeout(context));
                return true;
            }
            return false;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            context.Operation?.OnCancel();
            Answer(context, ErrorFor(e));
            return true;
        }
    }

    private void CancelMatching(IAgentConnection connection, string? id)
    {
        var closed = id is null;
        for (var i = 0; i < _queries.Count; i++)
        {
            if (Matches(_queries[i]))
            {
                Cancel(_queries[i]);
                _queries.RemoveAt(i--);
            }
        }
        for (var node = _mutations.First; node is not null;)
        {
            var next = node.Next;
            if (Matches(node.Value))
            {
                Cancel(node.Value);
                _mutations.Remove(node);
            }
            node = next;
        }

        bool Matches(AgentCallContext context) =>
            ReferenceEquals(context.Connection, connection) && (closed || context.Command.Id == id);

        void Cancel(AgentCallContext context)
        {
            context.Operation?.OnCancel();
            var response = AgentResponse.Err("", AgentErrorCodes.Cancelled, closed ? "The client went away." : "Cancelled by the client.");
            if (closed)
            {
                Stamp(context, response);
                Completed?.Invoke(context, response);
            }
            else
            {
                Answer(context, response);
            }
        }
    }

    private void Answer(AgentCallContext context, AgentResponse response)
    {
        Stamp(context, response);
        try
        {
            context.Connection.Send(response);
        }
        finally
        {
            Completed?.Invoke(context, response);
        }
    }

    private void Stamp(AgentCallContext context, AgentResponse response)
    {
        response.Id = context.Command.Id;
        response.DurationMs = context.Stopwatch.Elapsed.TotalMilliseconds;
        response.Timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        response.Frame = FrameSource?.Invoke();
    }

    private static AgentResponse ErrorFor(Exception e) => e switch
    {
        AgentException agent => AgentResponse.Err("", agent.Code, agent.Message, agent.Details),
        ArgumentException or FormatException or JsonException or InvalidCastException or KeyNotFoundException => AgentResponse.Err("", AgentErrorCodes.InvalidParams, e.Message),
        _ => AgentResponse.Err("", AgentErrorCodes.Internal, $"{e.GetType().Name}: {e.Message}"),
    };

    private sealed record Registration(ActionDefinition Definition, ActionKind Kind, Func<AgentCallContext, AgentOperation> Start);
}
