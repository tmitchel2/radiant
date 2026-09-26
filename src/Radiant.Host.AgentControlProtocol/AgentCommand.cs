using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// A command sent from the CLI to a running application instance: written as
/// <c>commands/cmd-&lt;id&gt;.json</c> by the file transport, or sent as one line by the socket transport.
/// </summary>
public sealed class AgentCommand
{
    /// <summary>Unique command identifier (used to correlate with response).</summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// The message type on the socket transport: <c>"req"</c> for a command, <c>"cancel"</c> to cancel
    /// the command <see cref="Id"/> names. Omitted by the file transport, where every file is a command.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>Action name, e.g. "ui.tap", "tab.list".</summary>
    public string Action { get; set; } = "";

    /// <summary>Action-specific parameters as raw JSON.</summary>
    public JsonElement? Params { get; set; }

    /// <summary>UTC timestamp in ISO 8601 format.</summary>
    public string Timestamp { get; set; } = "";

    /// <summary>
    /// How long the receiver may take before it answers <c>timeout</c>, in milliseconds; null for the
    /// receiver's default. Actions that wait (for idle, for an element) wait at most this long.
    /// </summary>
    public int? TimeoutMs { get; set; }
}
