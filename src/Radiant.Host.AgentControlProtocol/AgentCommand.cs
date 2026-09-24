using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// A command sent from the CLI to a running application instance.
/// Written as <c>commands/cmd-&lt;id&gt;.json</c>.
/// </summary>
public sealed class AgentCommand
{
    /// <summary>Unique command identifier (used to correlate with response).</summary>
    public string Id { get; set; } = "";

    /// <summary>Action name, e.g. "scene.load", "cfd.solve".</summary>
    public string Action { get; set; } = "";

    /// <summary>Action-specific parameters as raw JSON.</summary>
    public JsonElement? Params { get; set; }

    /// <summary>UTC timestamp in ISO 8601 format.</summary>
    public string Timestamp { get; set; } = "";
}
