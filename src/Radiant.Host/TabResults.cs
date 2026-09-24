using System.Text.Json.Serialization;

namespace Radiant.Host;

/// <summary>One entry in a <see cref="TabListResult"/>.</summary>
internal sealed class TabInfo
{
    /// <summary>Zero-based position in the tab strip.</summary>
    public int Index { get; set; }

    /// <summary>Instance name of the renderer backing this tab.</summary>
    public string Name { get; set; } = "";
}

/// <summary>Result of <c>tab.list</c> / <c>tab.activate</c> / <c>tab.close</c>: the current tab set.</summary>
internal sealed class TabListResult
{
    /// <summary>Index of the active tab, or -1 when there are no tabs.</summary>
    public int ActiveIndex { get; set; }

    /// <summary>Name of the active tab, or null when there are no tabs.</summary>
    public string? Active { get; set; }

    /// <summary>All tabs in strip order.</summary>
    public TabInfo[] Tabs { get; set; } = [];
}

/// <summary>Result of <c>tab.spawn</c>: the launched renderer's instance name and process id.</summary>
internal sealed class TabSpawnResult
{
    /// <summary>Instance name the spawned renderer was launched with (becomes a tab once it publishes frames).</summary>
    public string Name { get; set; } = "";

    /// <summary>Process id of the spawned renderer.</summary>
    public int Pid { get; set; }
}

/// <summary>AOT-safe serializer context for the host's <c>tab.*</c> result payloads.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TabListResult))]
[JsonSerializable(typeof(TabSpawnResult))]
[JsonSerializable(typeof(WindowBounds))]
[JsonSerializable(typeof(DragSessionState))]
internal sealed partial class HostJsonContext : JsonSerializerContext;
