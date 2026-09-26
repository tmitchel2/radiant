using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// Which elements of a UI an action means: every test that's set must pass. In JSON either this object or
/// a string in the compact syntax <see cref="SelectorSyntax"/> reads, e.g. <c>"@save"</c>,
/// <c>"role=button label=\"Save\""</c> or <c>"@orders >> text~=delete [1]"</c>.
/// </summary>
[JsonConverter(typeof(SelectorJsonConverter))]
public sealed record Selector
{
    /// <summary>The element's test ID (<c>Element.TestId</c>).</summary>
    public string? TestId { get; init; }

    /// <summary>The node's id, as a tree or inspect result gave it. Only good for the life of the node.</summary>
    public int? Id { get; init; }

    /// <summary>The node's semantics role, e.g. <c>button</c>, <c>textField</c>; case and <c>-</c>/<c>_</c> are ignored.</summary>
    public string? Role { get; init; }

    /// <summary>The node's accessible label.</summary>
    public TextMatch? Label { get; init; }

    /// <summary>The node's label or its value: what it shows.</summary>
    public TextMatch? Text { get; init; }

    /// <summary>The node's value: a text field's text, a slider's position.</summary>
    public TextMatch? Value { get; init; }

    /// <summary>Whether any of it is in view.</summary>
    public bool? Visible { get; init; }

    /// <summary>Whether it's enabled.</summary>
    public bool? Enabled { get; init; }

    /// <summary>Whether it has keyboard focus.</summary>
    public bool? Focused { get; init; }

    /// <summary>Whether it's checked.</summary>
    public bool? Checked { get; init; }

    /// <summary>Whether it's selected.</summary>
    public bool? Selected { get; init; }

    /// <summary>Which of the matches, from 0 in tree order; negative counts from the end.</summary>
    public int? Index { get; init; }

    /// <summary>An element the match must be inside.</summary>
    public Selector? Within { get; init; }

    /// <summary>An element the match must have inside it: a dialog that has "Confirm" in it.</summary>
    public Selector? Has { get; init; }

    /// <summary>Parses the compact syntax; see <see cref="SelectorSyntax"/>.</summary>
    public static Selector Parse(string text) => SelectorSyntax.Parse(text);

    /// <summary>The selector in the compact syntax, which <see cref="Parse"/> reads back.</summary>
    public override string ToString() => SelectorSyntax.Format(this);

    /// <summary>A copy with a different <see cref="Index"/>.</summary>
    public Selector WithIndex(int? index) => this with { Index = index };
}

/// <summary>Reads a <see cref="Selector"/> from the compact syntax or an object, and writes an object.</summary>
public sealed class SelectorJsonConverter : JsonConverter<Selector>
{
    /// <inheritdoc />
    public override Selector? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            try
            {
                return SelectorSyntax.Parse(reader.GetString()!);
            }
            catch (FormatException e)
            {
                throw new JsonException(e.Message, e);
            }
        }
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("A selector is a string or an object.");
        }
        var textMatch = new TextMatchJsonConverter();
        var selector = new Selector();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            if (reader.TokenType == JsonTokenType.Null)
            {
                continue;
            }
            selector = name switch
            {
                "testId" => selector with { TestId = reader.GetString() },
                "id" => selector with { Id = reader.GetInt32() },
                "role" => selector with { Role = reader.GetString() },
                "label" => selector with { Label = textMatch.Read(ref reader, typeof(TextMatch), options) },
                "text" => selector with { Text = textMatch.Read(ref reader, typeof(TextMatch), options) },
                "value" => selector with { Value = textMatch.Read(ref reader, typeof(TextMatch), options) },
                "visible" => selector with { Visible = reader.GetBoolean() },
                "enabled" => selector with { Enabled = reader.GetBoolean() },
                "focused" => selector with { Focused = reader.GetBoolean() },
                "checked" => selector with { Checked = reader.GetBoolean() },
                "selected" => selector with { Selected = reader.GetBoolean() },
                "index" => selector with { Index = reader.GetInt32() },
                "within" => selector with { Within = Read(ref reader, typeToConvert, options) },
                "has" => selector with { Has = Read(ref reader, typeToConvert, options) },
                _ => Skip(ref reader, selector),
            };
        }
        return selector;

        static Selector Skip(ref Utf8JsonReader reader, Selector selector)
        {
            reader.Skip();
            return selector;
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Selector value, JsonSerializerOptions options)
    {
        var textMatch = new TextMatchJsonConverter();
        writer.WriteStartObject();
        if (value.TestId is { } testId)
        {
            writer.WriteString("testId", testId);
        }
        if (value.Id is { } id)
        {
            writer.WriteNumber("id", id);
        }
        if (value.Role is { } role)
        {
            writer.WriteString("role", role);
        }
        WriteMatch("label", value.Label);
        WriteMatch("text", value.Text);
        WriteMatch("value", value.Value);
        WriteFlag("visible", value.Visible);
        WriteFlag("enabled", value.Enabled);
        WriteFlag("focused", value.Focused);
        WriteFlag("checked", value.Checked);
        WriteFlag("selected", value.Selected);
        if (value.Index is { } index)
        {
            writer.WriteNumber("index", index);
        }
        if (value.Within is { } within)
        {
            writer.WritePropertyName("within");
            Write(writer, within, options);
        }
        if (value.Has is { } has)
        {
            writer.WritePropertyName("has");
            Write(writer, has, options);
        }
        writer.WriteEndObject();

        void WriteMatch(string name, TextMatch? match)
        {
            if (match is not null)
            {
                writer.WritePropertyName(name);
                textMatch.Write(writer, match, options);
            }
        }

        void WriteFlag(string name, bool? flag)
        {
            if (flag is { } set)
            {
                writer.WriteBoolean(name, set);
            }
        }
    }
}

/// <summary>
/// The compact selector syntax, for command lines and tests. Terms separated by spaces must all pass;
/// <c>A &gt;&gt; B</c> is B inside A.
/// <list type="bullet">
/// <item><c>@save</c> or <c>testId=save</c>: the test ID.</item>
/// <item><c>#412</c> or <c>id=412</c>: the node id.</item>
/// <item><c>role=button</c>: the role.</item>
/// <item><c>label="Save"</c>, <c>text=Save</c>, <c>value=…</c>: exact; <c>~=</c> contains ignoring case;
/// <c>*=</c> contains; <c>^=</c> equals ignoring case; <c>=/re/</c> or <c>=/re/i</c> a regular expression.</item>
/// <item><c>visible</c>, <c>enabled</c>, <c>focused</c>, <c>checked</c>, <c>selected</c>, or <c>checked=false</c>.</item>
/// <item><c>[2]</c>: the third match; <c>[-1]</c> the last.</item>
/// <item><c>has(text=Confirm)</c>: one with a match for the selector inside it.</item>
/// <item>anything else, bare or quoted: <c>text=</c> it.</item>
/// </list>
/// </summary>
public static class SelectorSyntax
{
    private static readonly string[] s_flags = ["visible", "enabled", "focused", "checked", "selected"];

    /// <summary>Parses <paramref name="text"/>; throws <see cref="FormatException"/> saying where it's wrong.</summary>
    public static Selector Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokens = Tokenize(text);
        if (tokens.Count == 0)
        {
            throw new FormatException("The selector is empty.");
        }
        Selector? scope = null;
        var current = new Selector();
        var terms = 0;
        foreach (var token in tokens)
        {
            if (token.Kind == TokenKind.Within)
            {
                if (terms == 0)
                {
                    throw new FormatException($"'>>' at {token.Position} has nothing before it.");
                }
                scope = current with { Within = scope };
                current = new Selector();
                terms = 0;
                continue;
            }
            current = Apply(current, token);
            terms++;
        }
        if (terms == 0)
        {
            throw new FormatException("The selector ends with '>>'.");
        }
        return current with { Within = scope };
    }

    /// <summary>True and the selector if <paramref name="text"/> parses.</summary>
    public static bool TryParse(string text, out Selector? selector)
    {
        try
        {
            selector = Parse(text);
            return true;
        }
        catch (FormatException)
        {
            selector = null;
            return false;
        }
    }

    /// <summary>Writes <paramref name="selector"/> in the compact syntax.</summary>
    public static string Format(Selector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var terms = new List<string>();
        if (selector.Within is { } within)
        {
            terms.Add(Format(within));
            terms.Add(">>");
        }
        if (selector.TestId is { } testId)
        {
            terms.Add(IsBare(testId) ? "@" + testId : "testId=" + Quote(testId));
        }
        if (selector.Id is { } id)
        {
            terms.Add("#" + id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        if (selector.Role is { } role)
        {
            terms.Add("role=" + Quote(role));
        }
        if (selector.Label is { } label)
        {
            terms.Add("label" + label);
        }
        if (selector.Text is { } match)
        {
            terms.Add("text" + match);
        }
        if (selector.Value is { } value)
        {
            terms.Add("value" + value);
        }
        AddFlag("visible", selector.Visible);
        AddFlag("enabled", selector.Enabled);
        AddFlag("focused", selector.Focused);
        AddFlag("checked", selector.Checked);
        AddFlag("selected", selector.Selected);
        if (selector.Has is { } has)
        {
            terms.Add("has(" + Format(has) + ")");
        }
        if (selector.Index is { } index)
        {
            terms.Add("[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]");
        }
        return terms.Count == 0 ? "*" : string.Join(' ', terms);

        void AddFlag(string name, bool? flag)
        {
            if (flag is { } set)
            {
                terms.Add(set ? name : name + "=false");
            }
        }
    }

    /// <summary><paramref name="value"/> bare if it can be, otherwise double-quoted with <c>\</c> escapes.</summary>
    public static string Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (IsBare(value))
        {
            return value;
        }
        var builder = new StringBuilder("\"");
        foreach (var c in value)
        {
            if (c is '"' or '\\')
            {
                builder.Append('\\');
            }
            builder.Append(c);
        }
        return builder.Append('"').ToString();
    }

    // Bare values are identifiers of a sort: nothing the tokenizer or the flags would read differently.
    private static bool IsBare(string value) =>
        value.Length > 0
        && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or ':')
        && !s_flags.Contains(value, StringComparer.Ordinal)
        && value is not ("true" or "false");

    private static Selector Apply(Selector selector, Token token)
    {
        switch (token.Kind)
        {
            case TokenKind.Index:
                return selector with { Index = ParseInt(token.Value, token.Position) };
            case TokenKind.Quoted:
                return selector with { Text = TextMatch.Exact(token.Value) };
            case TokenKind.Has:
                return selector with { Has = Parse(token.Value) };
        }
        var term = token.Value;
        if (term == "*")
        {
            return selector;
        }
        if (term.StartsWith('@') && term.Length > 1)
        {
            return selector with { TestId = term[1..] };
        }
        if (term.StartsWith('#') && term.Length > 1)
        {
            return selector with { Id = ParseInt(term[1..], token.Position) };
        }
        if (s_flags.Contains(term, StringComparer.Ordinal))
        {
            return SetFlag(selector, term, true, token.Position);
        }
        var (key, op, value, isRegex, flags) = SplitTerm(term, token);
        if (key is null)
        {
            return selector with { Text = TextMatch.Exact(Unquote(term)) };
        }
        switch (key)
        {
            case "testId":
                return selector with { TestId = RequireEquals(value) };
            case "id":
                return selector with { Id = ParseInt(RequireEquals(value), token.Position) };
            case "role":
                return selector with { Role = RequireEquals(value) };
            case "index":
                return selector with { Index = ParseInt(RequireEquals(value), token.Position) };
            case "label":
                return selector with { Label = Match() };
            case "text":
                return selector with { Text = Match() };
            case "value":
                return selector with { Value = Match() };
        }
        if (s_flags.Contains(key, StringComparer.Ordinal))
        {
            return RequireEquals(value) switch
            {
                "true" => SetFlag(selector, key, true, token.Position),
                "false" => SetFlag(selector, key, false, token.Position),
                _ => throw new FormatException($"'{key}' at {token.Position} is true or false, not '{value}'."),
            };
        }
        throw new FormatException($"Unknown selector key '{key}' at {token.Position}; expected testId, id, role, label, text, value, index, {string.Join(", ", s_flags)}.");

        string RequireEquals(string v) => op == "=" && !isRegex
            ? v
            : throw new FormatException($"'{key}' at {token.Position} takes '=' and a plain value.");

        TextMatch Match() => isRegex
            ? new TextMatch(value, TextMatchMode.Regex, flags.Contains('i', StringComparison.Ordinal))
            : op switch
            {
                "~=" => new TextMatch(value, TextMatchMode.Contains, IgnoreCase: true),
                "*=" => new TextMatch(value, TextMatchMode.Contains),
                "^=" => new TextMatch(value, TextMatchMode.Exact, IgnoreCase: true),
                _ => TextMatch.Exact(value),
            };
    }

    private static Selector SetFlag(Selector selector, string name, bool value, int position) => name switch
    {
        "visible" => selector with { Visible = value },
        "enabled" => selector with { Enabled = value },
        "focused" => selector with { Focused = value },
        "checked" => selector with { Checked = value },
        "selected" => selector with { Selected = value },
        _ => throw new FormatException($"Unknown flag '{name}' at {position}."),
    };

    private static int ParseInt(string text, int position) =>
        int.TryParse(text, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"'{text}' at {position} isn't a whole number.");

    // key op value, where op is =, ~=, *= or ^= and value is bare, quoted or /regex/flags.
    private static (string? Key, string Op, string Value, bool IsRegex, string Flags) SplitTerm(string term, Token token)
    {
        var eq = term.IndexOf('=', StringComparison.Ordinal);
        if (eq <= 0 || term.StartsWith('"'))
        {
            return (null, "", "", false, "");
        }
        var keyEnd = term[eq - 1] is '~' or '*' or '^' ? eq - 1 : eq;
        var key = term[..keyEnd];
        if (key.Length == 0 || !key.All(char.IsLetter))
        {
            return (null, "", "", false, "");
        }
        var op = term[keyEnd..(eq + 1)];
        var raw = term[(eq + 1)..];
        if (raw.Length >= 2 && raw[0] == '/')
        {
            var close = raw.LastIndexOf('/');
            if (close == 0)
            {
                throw new FormatException($"The regular expression at {token.Position} has no closing '/'.");
            }
            if (op != "=")
            {
                throw new FormatException($"A regular expression at {token.Position} takes '=', not '{op}'.");
            }
            return (key, op, raw[1..close].Replace("\\/", "/", StringComparison.Ordinal), true, raw[(close + 1)..]);
        }
        return (key, op, Unquote(raw), false, "");
    }

    private static string Unquote(string value)
    {
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            return value;
        }
        var builder = new StringBuilder();
        for (var i = 1; i < value.Length - 1; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length - 1)
            {
                i++;
            }
            builder.Append(value[i]);
        }
        return builder.ToString();
    }

    private enum TokenKind
    {
        Term,
        Quoted,
        Index,
        Within,
        Has,
    }

    private readonly record struct Token(TokenKind Kind, string Value, int Position);

    // Splits on spaces outside quotes and regular expressions; '[n]' and '>>' are tokens of their own.
    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                i++;
                continue;
            }
            var start = i;
            if (text[i] == '>' && i + 1 < text.Length && text[i + 1] == '>')
            {
                tokens.Add(new Token(TokenKind.Within, ">>", start));
                i += 2;
                continue;
            }
            if (text[i] == '[')
            {
                var close = text.IndexOf(']', i);
                if (close < 0)
                {
                    throw new FormatException($"'[' at {start} has no closing ']'.");
                }
                tokens.Add(new Token(TokenKind.Index, text[(i + 1)..close].Trim(), start));
                i = close + 1;
                continue;
            }
            if (text[i] == '"')
            {
                var (value, end) = ReadQuoted(text, i);
                tokens.Add(new Token(TokenKind.Quoted, value, start));
                i = end;
                continue;
            }
            if (string.CompareOrdinal(text, i, "has(", 0, 4) == 0)
            {
                var close = ClosingParenthesis(text, i + 3);
                tokens.Add(new Token(TokenKind.Has, text[(i + 4)..close], start));
                i = close + 1;
                continue;
            }
            var builder = new StringBuilder();
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '[' && !(text[i] == '>' && i + 1 < text.Length && text[i + 1] == '>'))
            {
                if (text[i] == '"')
                {
                    var (_, end) = ReadQuoted(text, i);
                    builder.Append(text, i, end - i);
                    i = end;
                    continue;
                }
                if (text[i] == '/' && i > start && text[i - 1] == '=')
                {
                    var end = i + 1;
                    while (end < text.Length && text[end] != '/')
                    {
                        end += text[end] == '\\' ? 2 : 1;
                    }
                    if (end >= text.Length)
                    {
                        throw new FormatException($"The regular expression at {i} has no closing '/'.");
                    }
                    end++;
                    while (end < text.Length && char.IsLetter(text[end]))
                    {
                        end++;
                    }
                    builder.Append(text, i, end - i);
                    i = end;
                    continue;
                }
                builder.Append(text[i]);
                i++;
            }
            tokens.Add(new Token(TokenKind.Term, builder.ToString(), start));
        }
        return tokens;
    }

    // The ')' that closes the '(' at open, passing over quoted text and nested parentheses.
    private static int ClosingParenthesis(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '"':
                    i = ReadQuoted(text, i).End - 1;
                    break;
                case '(':
                    depth++;
                    break;
                case ')' when --depth == 0:
                    return i;
            }
        }
        throw new FormatException($"'(' at {open} has no closing ')'.");
    }

    private static (string Value, int End) ReadQuoted(string text, int open)
    {
        var builder = new StringBuilder();
        var i = open + 1;
        while (i < text.Length && text[i] != '"')
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                i++;
            }
            builder.Append(text[i]);
            i++;
        }
        if (i >= text.Length)
        {
            throw new FormatException($"The quote at {open} isn't closed.");
        }
        return (builder.ToString(), i + 1);
    }
}
