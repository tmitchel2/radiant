using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// The socket transport on the client's side: connects to an instance's <see cref="AgentSocketServer"/>,
/// reads its <see cref="Hello"/>, and sends commands, any number at once, each answered by id.
/// </summary>
public sealed class AgentSocketClient : IAgentClient, IDisposable
{
    // How much longer than a command's own timeout the client waits, for the server to say so itself.
    private const int GraceMs = 2000;

    private readonly Socket _socket;
    private readonly NetworkStream _stream;
    private readonly StreamReader _reader;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<AgentResponse>> _pending = new(StringComparer.Ordinal);
    private readonly Channel<AgentEvent> _events = Channel.CreateUnbounded<AgentEvent>();
    private readonly CancellationTokenSource _stop = new();
    private Task? _reading;
    private int _nextId;
    private string? _instance;

    private AgentSocketClient(Socket socket)
    {
        _socket = socket;
        _stream = new NetworkStream(socket, ownsSocket: false);
        _reader = new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);
    }

    /// <summary>The server's greeting.</summary>
    public AgentHello Hello { get; private set; } = new();

    /// <summary>Events the server pushes, for subscriptions this client made.</summary>
    public ChannelReader<AgentEvent> Events => _events.Reader;

    ChannelReader<AgentEvent>? IAgentClient.Events => _events.Reader;

    /// <summary>The instance's name: as it greeted, unless the one connecting knew it.</summary>
    public string Instance
    {
        get => _instance ?? Hello.Instance;
        set => _instance = value;
    }

    /// <inheritdoc/>
    public string Transport => "socket";

    /// <summary>True until the server goes away.</summary>
    public bool IsConnected => _reading is { IsCompleted: false };

    /// <summary>Connects to the socket at <paramref name="socketPath"/> and reads its greeting.</summary>
    public static async Task<AgentSocketClient> ConnectAsync(string socketPath, CancellationToken cancellation = default)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellation).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
        var client = new AgentSocketClient(socket);
        try
        {
            var line = await client._reader.ReadLineAsync(cancellation).ConfigureAwait(false)
                ?? throw new IOException("The instance closed the connection before greeting.");
            client.Hello = JsonSerializer.Deserialize(line, AgentJsonContext.Compact.AgentHello)
                ?? throw new IOException("The instance's greeting was empty.");
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        client._reading = Task.Run(client.ReadAsync, CancellationToken.None);
        return client;
    }

    /// <summary>
    /// Sends a command and waits for its response: the server's, or a <c>timeout</c> error if none comes
    /// a little after <paramref name="timeoutMs"/>, or <c>unreachable</c> if the connection drops.
    /// Cancelling <paramref name="cancellation"/> cancels the command on the server too.
    /// </summary>
    public async Task<AgentResponse> SendAsync(string action, JsonElement? parameters = null, int? timeoutMs = null, CancellationToken cancellation = default)
    {
        var id = "c" + Interlocked.Increment(ref _nextId).ToString(CultureInfo.InvariantCulture);
        var command = new AgentCommand
        {
            Type = "req",
            Id = id,
            Action = action,
            Params = parameters,
            TimeoutMs = timeoutMs,
            Timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
        };
        var completion = new TaskCompletionSource<AgentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;
        try
        {
            await WriteAsync(JsonSerializer.Serialize(command, AgentJsonContext.Compact.AgentCommand), cancellation).ConfigureAwait(false);
            var wait = (timeoutMs ?? AgentProtocol.DefaultTimeoutMs) + GraceMs;
            try
            {
                return await completion.Task.WaitAsync(TimeSpan.FromMilliseconds(wait), cancellation).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                await TryCancelAsync(id).ConfigureAwait(false);
                return AgentResponse.Err(id, AgentErrorCodes.Timeout, $"No response to {action} within {wait}ms.");
            }
            catch (OperationCanceledException)
            {
                await TryCancelAsync(id).ConfigureAwait(false);
                throw;
            }
        }
        catch (IOException e)
        {
            return AgentResponse.Err(id, AgentErrorCodes.Unreachable, e.Message);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _socket.Dispose();
        if (_reading is not null)
        {
            try
            {
                await _reading.ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException or SocketException)
            {
            }
        }
        _reader.Dispose();
        await _stream.DisposeAsync().ConfigureAwait(false);
        _writeLock.Dispose();
        _stop.Dispose();
    }

    /// <inheritdoc />
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private async Task TryCancelAsync(string id)
    {
        try
        {
            var cancel = new AgentCommand { Type = "cancel", Id = id };
            await WriteAsync(JsonSerializer.Serialize(cancel, AgentJsonContext.Compact.AgentCommand), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or SocketException)
        {
        }
    }

    private async Task WriteAsync(string line, CancellationToken cancellation)
    {
        await _writeLock.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"), cancellation).ConfigureAwait(false);
            await _stream.FlushAsync(cancellation).ConfigureAwait(false);
        }
        catch (ObjectDisposedException e)
        {
            throw new IOException("The connection is closed.", e);
        }
        catch (SocketException e)
        {
            throw new IOException(e.Message, e);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(_stop.Token).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }
                Receive(line);
            }
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException or SocketException)
        {
        }
        finally
        {
            _events.Writer.TryComplete();
            foreach (var (id, pending) in _pending)
            {
                pending.TrySetResult(AgentResponse.Err(id, AgentErrorCodes.Unreachable, "The instance closed the connection."));
            }
        }
    }

    private void Receive(string line)
    {
        using var document = JsonDocument.Parse(line);
        var type = document.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (type == "evt")
        {
            if (document.RootElement.Deserialize(AgentJsonContext.Compact.AgentEvent) is { } agentEvent)
            {
                _events.Writer.TryWrite(agentEvent);
            }
            return;
        }
        if (document.RootElement.Deserialize(AgentJsonContext.Compact.AgentResponse) is { } response
            && _pending.TryGetValue(response.Id, out var completion))
        {
            completion.TrySetResult(response);
        }
    }
}
