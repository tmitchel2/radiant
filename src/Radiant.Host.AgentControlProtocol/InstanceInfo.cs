namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// Registration metadata for a running application instance.
/// Serialized to <c>&lt;data directory&gt;/instances/&lt;name&gt;/instance.json</c>.
/// </summary>
public sealed class InstanceInfo
{
    /// <summary>Unique instance name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Process ID of the running instance.</summary>
    public int Pid { get; set; }

    /// <summary>UTC start time in ISO 8601 format.</summary>
    public string StartTime { get; set; } = "";

    /// <summary>Working directory of the running instance.</summary>
    public string WorkingDirectory { get; set; } = "";

    /// <summary>Path to the debug sync state directory.</summary>
    public string StateDirectory { get; set; } = "";

    /// <summary>Available action categories.</summary>
    public string[] Capabilities { get; set; } = [];

    /// <summary>
    /// Multi-process tab protocol version this instance speaks (<c>Radiant.Host.Ipc.TabProtocol.Version</c>),
    /// or 0 for a build that predates tab support. The compositing host compares this against its own
    /// protocol version before adopting the instance as a tab. 0 / a mismatch means "not a compatible
    /// tab renderer" and the host skips it with a clear message.
    /// </summary>
    public int ProtocolVersion { get; set; }

    /// <summary>
    /// The agent protocol version this instance answers (<see cref="AgentProtocol.Version"/>), or 0 for
    /// one that predates it: the version of the <c>ui.*</c>/<c>app.*</c> actions and the socket transport.
    /// </summary>
    public int AgentProtocolVersion { get; set; }

    /// <summary>
    /// What the instance is: <c>"ui"</c> for an application answering UI automation, <c>"host"</c> for the
    /// compositing host. Null for an instance that predates it.
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>The application's name, for listing instances.</summary>
    public string? AppName { get; set; }

    /// <summary>The transports commands reach it by: <c>"file"</c>, <c>"socket"</c>. Null means file only.</summary>
    public string[]? Transports { get; set; }

    /// <summary>The Unix domain socket the socket transport listens on, if it has one.</summary>
    public string? SocketPath { get; set; }

    /// <summary>The interaction log it writes, if it writes one.</summary>
    public string? LogPath { get; set; }

    /// <summary>True if it runs without a window.</summary>
    public bool Headless { get; set; }

    /// <summary>Its clock: <c>"real"</c>, or <c>"fixed"</c> when each frame steps a fixed time.</summary>
    public string? Clock { get; set; }

    /// <summary>True once it's ready for commands: for a UI application, once its tree is first mounted.</summary>
    public bool Ready { get; set; }
}
