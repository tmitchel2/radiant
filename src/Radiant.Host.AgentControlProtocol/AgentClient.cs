using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>Which transport a client talks to an instance over.</summary>
public enum AgentTransport
{
    /// <summary>The socket if the instance has one, else files.</summary>
    Auto,

    /// <summary>The Unix domain socket: fast, and it can stream events.</summary>
    Socket,

    /// <summary>Command and response files: slower, but plain files an agent can also read and write itself.</summary>
    File,
}

/// <summary>A connection to a running instance, over whichever transport.</summary>
public interface IAgentClient : IAsyncDisposable
{
    /// <summary>The instance's name.</summary>
    string Instance { get; }

    /// <summary>What the instance said about itself.</summary>
    AgentHello Hello { get; }

    /// <summary><c>socket</c>, <c>file</c> or <c>in-process</c>.</summary>
    string Transport { get; }

    /// <summary>Events for this client's subscriptions; null if the transport can't push them.</summary>
    ChannelReader<AgentEvent>? Events { get; }

    /// <summary>Sends a command and waits for its response (an error response if it times out or can't be delivered).</summary>
    Task<AgentResponse> SendAsync(string action, JsonElement? parameters = null, int? timeoutMs = null, CancellationToken cancellation = default);
}

/// <summary>Connects to instances.</summary>
public static class AgentClient
{
    /// <summary>
    /// Connects to the running instance <paramref name="instance"/> (under <see cref="InstanceRegistry.RootDir"/>)
    /// over <paramref name="transport"/>. Throws <see cref="InvalidOperationException"/> if it isn't running or
    /// doesn't offer that transport.
    /// </summary>
    public static async Task<IAgentClient> ConnectAsync(string instance, AgentTransport transport = AgentTransport.Auto, CancellationToken cancellation = default)
    {
        var info = InstanceRegistry.GetInstance(instance)
            ?? throw new InvalidOperationException($"No running instance '{instance}' under {InstanceRegistry.RootDir}.");
        var socket = info.SocketPath is { Length: > 0 } path && (info.Transports?.Contains("socket") ?? false) ? path : null;
        if (transport == AgentTransport.Socket && socket is null)
        {
            throw new InvalidOperationException($"'{instance}' has no socket transport.");
        }
        if (transport != AgentTransport.File && socket is not null)
        {
            try
            {
                var client = await AgentSocketClient.ConnectAsync(socket, cancellation).ConfigureAwait(false);
                client.Instance = instance;
                return client;
            }
            catch (Exception e) when (transport == AgentTransport.Auto && e is IOException or System.Net.Sockets.SocketException)
            {
                // Fall back to files.
            }
        }
        return new FileAgentClient(info);
    }

    /// <summary>
    /// The instance to use when none is named: the one given by <c>$RADIANT_AGENT_INSTANCE</c>, else the
    /// only running instance that answers UI automation. Throws <see cref="InvalidOperationException"/>
    /// saying which are running when that's not one.
    /// </summary>
    public static string ResolveInstance(string? name)
    {
        if (!string.IsNullOrEmpty(name))
        {
            return name;
        }
        if (Environment.GetEnvironmentVariable("RADIANT_AGENT_INSTANCE") is { Length: > 0 } fromEnvironment)
        {
            return fromEnvironment;
        }
        var all = InstanceRegistry.ListInstances();
        var ui = all.Where(i => i.Kind == "ui").ToArray();
        return ui.Length switch
        {
            1 => ui[0].Name,
            0 => throw new InvalidOperationException(all.Length == 0
                ? $"No instances are running (under {InstanceRegistry.RootDir}). Launch one first."
                : $"No UI instance is running; there are: {string.Join(", ", all.Select(i => i.Name))}."),
            _ => throw new InvalidOperationException($"Several UI instances are running; name one: {string.Join(", ", ui.Select(i => i.Name))}."),
        };
    }
}

/// <summary>The file transport as an <see cref="IAgentClient"/>.</summary>
public sealed class FileAgentClient : IAgentClient
{
    private readonly CommandClient _client;

    /// <summary>A client for the instance <paramref name="info"/> describes.</summary>
    public FileAgentClient(InstanceInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        Instance = info.Name;
        _client = new CommandClient(info.Name);
        Hello = new AgentHello
        {
            Instance = info.Name,
            App = info.AppName,
            Protocol = info.AgentProtocolVersion,
            Capabilities = info.Capabilities,
            Clock = info.Clock,
            Headless = info.Headless,
        };
    }

    /// <inheritdoc/>
    public string Instance { get; }

    /// <inheritdoc/>
    public AgentHello Hello { get; }

    /// <inheritdoc/>
    public string Transport => "file";

    /// <inheritdoc/>
    public ChannelReader<AgentEvent>? Events => null;

    /// <inheritdoc/>
    public Task<AgentResponse> SendAsync(string action, JsonElement? parameters = null, int? timeoutMs = null, CancellationToken cancellation = default)
    {
        var timeout = timeoutMs ?? AgentProtocol.DefaultTimeoutMs;
        // The receiver times out itself at its own default; asking for longer goes in the params' envelope.
        var paramsJson = parameters is { } p ? p.GetRawText() : null;
        return Task.Run(() => _client.Send(action, paramsJson, timeout + 2000, timeoutMs), cancellation);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>For logging.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"file:{Instance}");
}
