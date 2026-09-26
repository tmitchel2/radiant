namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// The file transport on the dispatcher's side: command files dropped in the instance's
/// <c>commands/</c> directory are queued on an <see cref="AgentDispatcher"/> as they arrive, and each
/// response is written to <c>responses/</c>. It can't push events; a client reads the log file instead.
/// </summary>
public sealed class FileDropTransport : IDisposable
{
    private readonly CommandReceiver _receiver;
    private readonly AgentDispatcher _dispatcher;
    private readonly Connection _connection;
    private readonly Lock _lock = new();

    /// <summary>Starts taking the named instance's command files, including any already waiting.</summary>
    public FileDropTransport(string instanceName, AgentDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
        _receiver = new CommandReceiver(instanceName);
        _connection = new Connection(_receiver);
        _receiver.CommandArrived += Drain;
        Drain();
    }

    /// <summary>Waits until every response written has been read, or <paramref name="timeoutMs"/> passes.</summary>
    public void WaitForResponsesDelivered(int timeoutMs) => _receiver.WaitForResponsesDelivered(timeoutMs);

    /// <inheritdoc />
    public void Dispose()
    {
        _receiver.CommandArrived -= Drain;
        _receiver.Dispose();
    }

    private void Drain()
    {
        // The watcher raises on its own threads; one drain at a time keeps a file from being read twice.
        lock (_lock)
        {
            foreach (var command in _receiver.DrainPendingCommands())
            {
                _dispatcher.Enqueue(command, _connection);
            }
        }
    }

    private sealed class Connection(CommandReceiver receiver) : IAgentConnection
    {
        public string Client => "file";

        public bool CanStream => false;

        public void Send(AgentResponse response)
        {
            // The file transport's messages carry no type: each file is one response.
            response.Type = null;
            receiver.WriteResponse(response);
        }

        public void SendEvent(AgentEvent agentEvent)
        {
        }
    }
}
