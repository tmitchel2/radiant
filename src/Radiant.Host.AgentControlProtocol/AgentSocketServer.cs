using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// The socket transport on the dispatcher's side: a Unix domain socket carrying newline-delimited JSON.
/// Each client is greeted with an <see cref="AgentHello"/>, then sends <c>{"type":"req",…}</c> commands
/// and <c>{"type":"cancel","id":…}</c>; it gets <c>{"type":"res",…}</c> responses in the order they
/// finish, and <c>{"type":"evt",…}</c> events for its subscriptions. A client that disconnects has its
/// commands cancelled.
/// </summary>
public sealed class AgentSocketServer : IDisposable
{
    private readonly AgentDispatcher _dispatcher;
    private readonly Func<AgentHello> _hello;
    private readonly CancellationTokenSource _stop = new();
    private Socket? _listener;
    private int _nextClient;

    /// <summary>A server for <paramref name="dispatcher"/> on <paramref name="socketPath"/>, greeting with <paramref name="hello"/>.</summary>
    public AgentSocketServer(string socketPath, AgentDispatcher dispatcher, Func<AgentHello> hello)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(hello);
        SocketPath = socketPath;
        _dispatcher = dispatcher;
        _hello = hello;
    }

    /// <summary>The socket's path.</summary>
    public string SocketPath { get; }

    /// <summary>Raised on a background thread when a client disconnects, with its connection.</summary>
    public event Action<IAgentConnection>? ConnectionClosed;

    /// <summary>
    /// Binds the socket, replacing a stale one, readable and writable only by this user, and starts
    /// accepting clients in the background.
    /// </summary>
    public void Start()
    {
        var directory = Path.GetDirectoryName(SocketPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        if (File.Exists(SocketPath))
        {
            File.Delete(SocketPath);
        }
        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(SocketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        _listener.Listen(16);
        _ = Task.Run(AcceptAsync);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Cancel();
        _listener?.Dispose();
        try
        {
            File.Delete(SocketPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        _stop.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            Socket client;
            try
            {
                client = await _listener!.AcceptAsync(_stop.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }
            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(Socket socket)
    {
        var stream = new NetworkStream(socket, ownsSocket: true);
        var connection = new Connection($"socket#{Interlocked.Increment(ref _nextClient)}");
        var writing = connection.WriteAllAsync(stream, _stop.Token);
        try
        {
            connection.Write(JsonSerializer.Serialize(_hello(), AgentJsonContext.Compact.AgentHello));
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            while (!_stop.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(_stop.Token).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }
                if (line.Length > 0)
                {
                    Receive(connection, line);
                }
            }
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException or SocketException)
        {
        }
        finally
        {
            _dispatcher.CancelConnection(connection);
            ConnectionClosed?.Invoke(connection);
            connection.Complete();
            try
            {
                await writing.ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException or SocketException)
            {
            }
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void Receive(Connection connection, string line)
    {
        AgentCommand? command;
        try
        {
            command = JsonSerializer.Deserialize(line, AgentJsonContext.Compact.AgentCommand);
        }
        catch (JsonException e)
        {
            connection.Send(AgentResponse.Err("", AgentErrorCodes.ParseError, $"Unreadable message: {e.Message}"));
            return;
        }
        if (command is null)
        {
            return;
        }
        if (command.Type == "cancel")
        {
            _dispatcher.Cancel(connection, command.Id);
            return;
        }
        _dispatcher.Enqueue(command, connection);
    }

    // Messages are queued and written by one task, so the UI thread never waits on a slow client.
    private sealed class Connection(string client) : IAgentConnection
    {
        private readonly Channel<string> _outgoing = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

        public string Client { get; } = client;

        public bool CanStream => true;

        public void Send(AgentResponse response)
        {
            response.Type = "res";
            Write(JsonSerializer.Serialize(response, AgentJsonContext.Compact.AgentResponse));
        }

        public void SendEvent(AgentEvent agentEvent) =>
            Write(JsonSerializer.Serialize(agentEvent, AgentJsonContext.Compact.AgentEvent));

        public void Write(string line) => _outgoing.Writer.TryWrite(line);

        public void Complete() => _outgoing.Writer.TryComplete();

        public async Task WriteAllAsync(Stream stream, CancellationToken cancellation)
        {
            var newline = "\n"u8.ToArray();
            await foreach (var line in _outgoing.Reader.ReadAllAsync(cancellation).ConfigureAwait(false))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(line), cancellation).ConfigureAwait(false);
                await stream.WriteAsync(newline, cancellation).ConfigureAwait(false);
                await stream.FlushAsync(cancellation).ConfigureAwait(false);
            }
        }
    }
}
