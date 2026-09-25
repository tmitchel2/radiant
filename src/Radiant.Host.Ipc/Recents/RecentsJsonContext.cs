using System.Text.Json.Serialization;

namespace Radiant.Host.Ipc.Recents;

/// <summary>
/// Source-generated JSON serializer context for <c>&lt;data directory&gt;/recent.json</c>, kept separate so the
/// store is AOT-safe inside the NativeAOT-published app.
/// </summary>
/// <remarks>
/// The options here are load-bearing for on-disk compatibility, not style: source generation applies
/// <see cref="JsonKnownNamingPolicy.CamelCase"/> on read as well as write, and
/// <c>PropertyNameCaseInsensitive</c> is off by default — so changing the naming policy would make every
/// already-written <c>recent.json</c> silently deserialize to an empty list.
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RecentEntry))]
[JsonSerializable(typeof(RecentEntry[]))]
public sealed partial class RecentsJsonContext : JsonSerializerContext;
