using System.Text.Json;
using System.Text.Json.Serialization;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// Source-generated JSON serializer context for AOT-compatible agent protocol serialization: the
/// protocol's own types and the generic results in <c>ActionResults.cs</c>. An application serialises its
/// own action results through a context of its own, combined with this one.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(InstanceInfo))]
[JsonSerializable(typeof(InstanceInfo[]))]
[JsonSerializable(typeof(AgentCommand))]
[JsonSerializable(typeof(AgentResponse))]
[JsonSerializable(typeof(AgentError))]
[JsonSerializable(typeof(AgentHello))]
[JsonSerializable(typeof(AgentEvent))]
[JsonSerializable(typeof(ActionDefinition))]
[JsonSerializable(typeof(ActionDefinition[]))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(BoolResult))]
[JsonSerializable(typeof(ExitResult))]
[JsonSerializable(typeof(ScreenshotResult))]
[JsonSerializable(typeof(Selector))]
[JsonSerializable(typeof(TextMatch))]
[JsonSerializable(typeof(LogEntry))]
[JsonSerializable(typeof(string[]))]
public sealed partial class AgentJsonContext : JsonSerializerContext
{
    /// <summary>
    /// The same types written without indentation, so each message is one line: what the socket
    /// transport and the interaction log write.
    /// </summary>
    public static AgentJsonContext Compact => s_compact ??= new(new JsonSerializerOptions(Default.Options) { WriteIndented = false });

    // Made on first use: a static initializer here could run before the generated Default exists.
    private static AgentJsonContext? s_compact;
}
