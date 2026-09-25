namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// Metadata describing an available action on an application instance.
/// </summary>
public sealed class ActionDefinition
{
    /// <summary>Action name, e.g. "scene.load".</summary>
    public string Name { get; set; } = "";

    /// <summary>Human-readable description.</summary>
    public string Description { get; set; } = "";

    /// <summary>Category grouping, e.g. "scene", "camera", "cfd".</summary>
    public string Category { get; set; } = "";

    /// <summary>JSON Schema string for the params object. Null if no params needed.</summary>
    public string? ParamsSchema { get; set; }
}
