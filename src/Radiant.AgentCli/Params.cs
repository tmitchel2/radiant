using System.Buffers;
using System.Text.Json;

namespace Radiant.AgentCli;

/// <summary>Builds a params object: only the fields that were given.</summary>
internal sealed class Params
{
    private readonly List<(string Name, object Value)> _fields = [];

    public Params Set(string name, object? value)
    {
        if (value is not null)
        {
            _fields.Add((name, value));
        }
        return this;
    }

    public Params Point(string name, (float X, float Y)? point) =>
        point is { } p ? Set(name, new Dictionary<string, object> { ["x"] = p.X, ["y"] = p.Y }) : this;

    public JsonElement Build()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in _fields)
            {
                writer.WritePropertyName(name);
                Write(writer, value);
            }
            writer.WriteEndObject();
        }
        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static void Write(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case float number:
                writer.WriteNumberValue(number);
                break;
            case double number:
                writer.WriteNumberValue(number);
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            case Dictionary<string, object> map:
                writer.WriteStartObject();
                foreach (var (key, item) in map)
                {
                    writer.WritePropertyName(key);
                    Write(writer, item);
                }
                writer.WriteEndObject();
                break;
            default:
                throw new ArgumentException($"Can't write {value.GetType().Name}.", nameof(value));
        }
    }
}
