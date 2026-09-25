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
}
