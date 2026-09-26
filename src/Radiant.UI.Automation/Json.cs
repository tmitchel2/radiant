using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Radiant.Host.AgentControlProtocol;

namespace Radiant.UI.Automation;

/// <summary>JSON helpers for the actions.</summary>
internal static class Json
{
    public static JsonElement Element<T>(T value, JsonTypeInfo<T> type) => JsonSerializer.SerializeToElement(value, type);

    public static JsonElement Refs(IEnumerable<NodeRef> refs) => Element(refs.ToArray(), AutomationJsonContext.Default.NodeRefArray);

    public static AgentResponse Ok<T>(T value, JsonTypeInfo<T> type) => AgentResponse.Ok("", Element(value, type), 0);

    /// <summary>The command's params as <typeparamref name="T"/>; an <c>invalid_params</c> error if they don't fit.</summary>
    public static T Params<T>(AgentCallContext context, JsonTypeInfo<T> type) where T : new()
    {
        try
        {
            return context.Params.Deserialize(type) ?? new T();
        }
        catch (JsonException e)
        {
            throw new AgentException(AgentErrorCodes.InvalidParams, $"{context.Command.Action}: {e.Message}");
        }
    }

    /// <summary>Writes JSON by hand, for shapes that depend on what was asked.</summary>
    public static JsonElement Write(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }
        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    /// <summary>An object of string arrays, for details.</summary>
    public static JsonElement Details(params (string Name, IEnumerable<string> Values)[] fields) => Write(writer =>
    {
        writer.WriteStartObject();
        foreach (var (name, values) in fields)
        {
            writer.WriteStartArray(name);
            foreach (var value in values)
            {
                writer.WriteStringValue(value);
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    });
}
