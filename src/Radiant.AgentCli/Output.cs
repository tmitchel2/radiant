using System.Globalization;
using System.Text;
using System.Text.Json;
using Radiant.Host.AgentControlProtocol;

namespace Radiant.AgentCli;

/// <summary>How results read on a terminal: one short line per thing, most useful first.</summary>
internal static class Output
{
    public static string Json(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            element.WriteTo(writer);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>A node: <c>#412 button "Save" @save (400,284 80×32) focusable checked</c>.</summary>
    public static string Node(JsonElement node)
    {
        var text = new StringBuilder();
        if (node.TryGetProperty("id", out var id))
        {
            text.Append('#').Append(id.GetInt32()).Append(' ');
        }
        text.Append(String(node, "role") ?? String(node, "kind") ?? "node");
        if (String(node, "label") is { } label)
        {
            text.Append(" \"").Append(Truncate(label, 60)).Append('"');
        }
        if (String(node, "testId") is { } testId)
        {
            text.Append(" @").Append(testId);
        }
        if (String(node, "value") is { } value && value != String(node, "label"))
        {
            text.Append(" value=\"").Append(Truncate(value, 40)).Append('"');
        }
        if (node.TryGetProperty("bounds", out var bounds))
        {
            text.Append(' ').Append(Rect(bounds));
        }
        if (node.TryGetProperty("tap", out var tap) && tap.ValueKind == JsonValueKind.Object)
        {
            text.Append(" tap").Append(Point(tap));
        }
        foreach (var flag in new[] { "focusable", "focused", "disabled", "selected", "expanded" })
        {
            if (node.TryGetProperty(flag, out var set) && set.ValueKind == JsonValueKind.True)
            {
                text.Append(' ').Append(flag);
            }
        }
        if (node.TryGetProperty("checked", out var isChecked) && isChecked.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            text.Append(isChecked.GetBoolean() ? " checked" : " unchecked");
        }
        if (node.TryGetProperty("visible", out var visible) && visible.ValueKind == JsonValueKind.False)
        {
            text.Append(" hidden");
        }
        if (node.TryGetProperty("scroll", out var scroll) && scroll.ValueKind == JsonValueKind.Object)
        {
            text.Append(" scroll").Append(Point(scroll.GetProperty("offset"))).Append('/').Append(Point(scroll.GetProperty("max")));
        }
        return text.ToString();
    }

    /// <summary>The tree, one node a line, indented.</summary>
    public static void Tree(TextWriter writer, JsonElement document)
    {
        foreach (var root in document.GetProperty("nodes").EnumerateArray())
        {
            Walk(root, 0);
        }

        void Walk(JsonElement node, int depth)
        {
            writer.Write(new string(' ', depth * 2));
            writer.Write(Node(node));
            if (node.TryGetProperty("childCount", out var count))
            {
                writer.Write($" (+{count.GetInt32()})");
            }
            writer.WriteLine();
            if (node.TryGetProperty("children", out var children))
            {
                foreach (var child in children.EnumerateArray())
                {
                    Walk(child, depth + 1);
                }
            }
        }
    }

    /// <summary>What an action did: <c>ok ui.tap button "Save" @save at (412,300) · 3 frames</c>.</summary>
    public static string Action(string action, JsonElement result)
    {
        var text = new StringBuilder("ok ").Append(action);
        if (result.ValueKind != JsonValueKind.Object)
        {
            return text.ToString();
        }
        if (result.TryGetProperty("target", out var target) && target.ValueKind == JsonValueKind.Object)
        {
            text.Append(' ').Append(Node(Without(target, "bounds")));
        }
        if (result.TryGetProperty("at", out var at))
        {
            text.Append(" at ").Append(Point(at));
        }
        if (result.TryGetProperty("value", out var value))
        {
            text.Append(" value=\"").Append(value.GetString()).Append('"');
        }
        if (result.TryGetProperty("scroll", out var scroll))
        {
            text.Append(" scroll").Append(Point(scroll.GetProperty("offset"))).Append('/').Append(Point(scroll.GetProperty("max")));
        }
        if (result.TryGetProperty("focusedId", out var focused) && focused.GetInt32() != 0)
        {
            text.Append(" focus #").Append(focused.GetInt32());
        }
        if (result.TryGetProperty("frames", out var frames))
        {
            text.Append(" · ").Append(frames.GetInt64()).Append(" frames");
        }
        if (result.TryGetProperty("idle", out var idle) && idle.ValueKind == JsonValueKind.False)
        {
            text.Append(" · not idle");
        }
        return text.ToString();
    }

    public static string Rect(JsonElement rect) => string.Create(CultureInfo.InvariantCulture,
        $"({rect.GetProperty("x").GetSingle():0.#},{rect.GetProperty("y").GetSingle():0.#} {rect.GetProperty("w").GetSingle():0.#}×{rect.GetProperty("h").GetSingle():0.#})");

    public static string Point(JsonElement point) => string.Create(CultureInfo.InvariantCulture,
        $"({point.GetProperty("x").GetSingle():0.#},{point.GetProperty("y").GetSingle():0.#})");

    public static string? String(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public static string Instances(IEnumerable<InstanceInfo> instances)
    {
        var rows = instances.Select(i => new[]
        {
            i.Name,
            i.Pid.ToString(CultureInfo.InvariantCulture),
            i.AppName ?? "",
            i.Kind ?? "",
            i.Kind == "ui" ? (i.Headless ? "headless" : "window") : "",
            i.Clock ?? "",
            string.Join(',', i.Transports ?? ["file"]),
            i.Ready ? "ready" : (i.Kind == "ui" ? "starting" : ""),
        }).Prepend(["NAME", "PID", "APP", "KIND", "MODE", "CLOCK", "TRANSPORTS", "STATE"]).ToList();
        var widths = Enumerable.Range(0, 8).Select(c => rows.Max(r => r[c].Length)).ToArray();
        return string.Join('\n', rows.Select(r => string.Join("  ", r.Select((cell, c) => cell.PadRight(widths[c]))).TrimEnd()));
    }

    private static JsonElement Without(JsonElement node, string name)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in node.EnumerateObject().Where(p => p.Name != name))
            {
                property.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static string Truncate(string text, int length)
    {
        var line = text.ReplaceLineEndings(" ");
        return line.Length <= length ? line : line[..(length - 1)] + "…";
    }
}
