using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// The first line the socket transport sends a client: who it's talking to and what it can do.
/// </summary>
public sealed class AgentHello
{
    /// <summary>Always <c>"hello"</c>.</summary>
    public string Type { get; set; } = "hello";

    /// <summary>The server's <see cref="AgentProtocol.Version"/>.</summary>
    public int Protocol { get; set; } = AgentProtocol.Version;

    /// <summary>The instance's name.</summary>
    public string Instance { get; set; } = "";

    /// <summary>The application's name.</summary>
    public string? App { get; set; }

    /// <summary>The instance's capabilities, as in <see cref="InstanceInfo.Capabilities"/>.</summary>
    public string[] Capabilities { get; set; } = [];

    /// <summary>The instance's clock, <c>"real"</c> or <c>"fixed"</c>.</summary>
    public string? Clock { get; set; }

    /// <summary>True if the instance runs without a window.</summary>
    public bool Headless { get; set; }
}

/// <summary>
/// Something the server pushes without being asked, on the socket transport: an interaction-log entry
/// for a subscription, say.
/// </summary>
public sealed class AgentEvent
{
    /// <summary>Always <c>"evt"</c>.</summary>
    public string Type { get; set; } = "evt";

    /// <summary>What happened, e.g. <c>"log"</c>.</summary>
    public string Event { get; set; } = "";

    /// <summary>The subscription it's for.</summary>
    public string? Sub { get; set; }

    /// <summary>Its sequence number within the event stream.</summary>
    public long Seq { get; set; }

    /// <summary>The event's payload.</summary>
    public JsonElement? Data { get; set; }
}
