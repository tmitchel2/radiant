using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>How a <see cref="TextMatch"/> compares.</summary>
public enum TextMatchMode
{
    /// <summary>The whole text equals the value.</summary>
    Exact,

    /// <summary>The text contains the value.</summary>
    Contains,

    /// <summary>The value is a regular expression the text matches.</summary>
    Regex,
}

/// <summary>
/// A test of an element's text: exact, contains or a regular expression, optionally ignoring case. In
/// JSON a plain string is an exact match; otherwise <c>{"exact"|"contains"|"regex": "…", "ignoreCase": true}</c>.
/// </summary>
[JsonConverter(typeof(TextMatchJsonConverter))]
public sealed record TextMatch(string Value, TextMatchMode Mode = TextMatchMode.Exact, bool IgnoreCase = false)
{
    private Regex? _regex;

    /// <summary>An exact match.</summary>
    public static TextMatch Exact(string value) => new(value);

    /// <summary>A case-insensitive contains match.</summary>
    public static TextMatch Contains(string value) => new(value, TextMatchMode.Contains, IgnoreCase: true);

    /// <summary>True if <paramref name="text"/> passes; null text never does.</summary>
    public bool Matches(string? text)
    {
        if (text is null)
        {
            return false;
        }
        var comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return Mode switch
        {
            TextMatchMode.Exact => string.Equals(text, Value, comparison),
            TextMatchMode.Contains => text.Contains(Value, comparison),
            _ => (_regex ??= new Regex(Value, IgnoreCase ? RegexOptions.IgnoreCase | RegexOptions.CultureInvariant : RegexOptions.CultureInvariant)).IsMatch(text),
        };
    }

    /// <inheritdoc />
    public bool Equals(TextMatch? other) =>
        other is not null && Value == other.Value && Mode == other.Mode && IgnoreCase == other.IgnoreCase;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Value, Mode, IgnoreCase);

    /// <summary>The compact selector syntax for the match, after the key: <c>="Save"</c>, <c>~="sav"</c>, <c>=/^Save/i</c>.</summary>
    public override string ToString() => Mode switch
    {
        TextMatchMode.Regex => "=/" + Value.Replace("/", "\\/", StringComparison.Ordinal) + "/" + (IgnoreCase ? "i" : ""),
        TextMatchMode.Contains when IgnoreCase => "~=" + SelectorSyntax.Quote(Value),
        TextMatchMode.Contains => "*=" + SelectorSyntax.Quote(Value),
        _ when IgnoreCase => "^=" + SelectorSyntax.Quote(Value),
        _ => "=" + SelectorSyntax.Quote(Value),
    };
}

/// <summary>Reads a <see cref="TextMatch"/> from a string or an object, and writes the shorter.</summary>
public sealed class TextMatchJsonConverter : JsonConverter<TextMatch>
{
    /// <inheritdoc />
    public override TextMatch? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return TextMatch.Exact(reader.GetString()!);
        }
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("A text match is a string or an object.");
        }
        string? value = null;
        var mode = TextMatchMode.Exact;
        var ignoreCase = false;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "exact":
                    value = reader.GetString();
                    mode = TextMatchMode.Exact;
                    break;
                case "contains":
                    value = reader.GetString();
                    mode = TextMatchMode.Contains;
                    break;
                case "regex":
                    value = reader.GetString();
                    mode = TextMatchMode.Regex;
                    break;
                case "ignoreCase":
                    ignoreCase = reader.GetBoolean();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }
        return value is null ? throw new JsonException("A text match needs one of exact, contains or regex.") : new TextMatch(value, mode, ignoreCase);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TextMatch value, JsonSerializerOptions options)
    {
        if (value.Mode == TextMatchMode.Exact && !value.IgnoreCase)
        {
            writer.WriteStringValue(value.Value);
            return;
        }
        writer.WriteStartObject();
        writer.WriteString(value.Mode switch { TextMatchMode.Contains => "contains", TextMatchMode.Regex => "regex", _ => "exact" }, value.Value);
        if (value.IgnoreCase)
        {
            writer.WriteBoolean("ignoreCase", true);
        }
        writer.WriteEndObject();
    }
}
