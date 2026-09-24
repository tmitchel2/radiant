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
[JsonSerializable(typeof(ActionDefinition))]
[JsonSerializable(typeof(ActionDefinition[]))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(BoolResult))]
[JsonSerializable(typeof(ExitResult))]
[JsonSerializable(typeof(ScreenshotResult))]
[JsonSerializable(typeof(string[]))]
public sealed partial class AgentJsonContext : JsonSerializerContext;
