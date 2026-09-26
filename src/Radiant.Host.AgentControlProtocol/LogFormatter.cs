using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// Reads and writes interaction logs: one <see cref="LogEntry"/> per line as JSON, and one per line for
/// people, e.g. <c>10:00:01.234  #882  human  tap      button "Save" @save (412,300)</c>.
/// </summary>
public static class LogFormatter
{
    /// <summary>The entry as one line of JSON, without the newline.</summary>
    public static string ToJsonLine(LogEntry entry) => JsonSerializer.Serialize(entry, AgentJsonContext.Compact.LogEntry);

    /// <summary>The entry a line of JSON holds, or null if it holds none.</summary>
    public static LogEntry? FromJsonLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize(line, AgentJsonContext.Compact.LogEntry);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The entry as one line for people.</summary>
    public static string Format(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var time = DateTime.TryParse(entry.T, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)
            ? t.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
            : entry.T;
        var what = entry.Kind switch
        {
            LogKinds.Input => entry.Input?.Type ?? "input",
            LogKinds.Action or LogKinds.Result => entry.Action?.Name ?? entry.Kind,
            LogKinds.State => entry.State ?? "state",
            _ => entry.Kind,
        };
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"{time}  #{entry.Frame,-5} {entry.Src,-6} {what,-12} ");
        switch (entry.Kind)
        {
            case LogKinds.Input:
                AppendTarget(builder, entry.Target);
                AppendInput(builder, entry.Input);
                break;
            case LogKinds.Action:
                builder.Append(entry.Action?.Selector ?? "");
                if (entry.Action?.Client is { } client)
                {
                    builder.Append(CultureInfo.InvariantCulture, $"  ({client})");
                }
                break;
            case LogKinds.Result:
                AppendResult(builder, entry);
                break;
            case LogKinds.State:
                AppendTarget(builder, entry.Target);
                break;
            case LogKinds.Screenshot:
                builder.Append(entry.Screenshot);
                break;
            default:
                builder.Append(entry.Note);
                break;
        }
        return builder.ToString().TrimEnd();
    }

    /// <summary>An element as the log shows it: <c>button "Save" @save #412</c>.</summary>
    public static string Describe(LogTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var builder = new StringBuilder();
        AppendTarget(builder, target);
        return builder.ToString().TrimEnd();
    }

    private static void AppendTarget(StringBuilder builder, LogTarget? target)
    {
        if (target is null)
        {
            return;
        }
        builder.Append(target.Role ?? "node");
        if (target.Label is { } label)
        {
            builder.Append(CultureInfo.InvariantCulture, $" \"{Truncate(label, 40)}\"");
        }
        if (target.TestId is { } testId)
        {
            builder.Append(CultureInfo.InvariantCulture, $" @{testId}");
        }
        builder.Append(CultureInfo.InvariantCulture, $" #{target.Id} ");
    }

    private static void AppendInput(StringBuilder builder, LogInput? input)
    {
        if (input is null)
        {
            return;
        }
        if (input.Mods is { Length: > 0 } mods)
        {
            builder.Append(string.Join('+', mods)).Append('+');
        }
        if (input.Key is { } key)
        {
            builder.Append(key).Append(' ');
        }
        if (input.Text is { } text)
        {
            builder.Append(CultureInfo.InvariantCulture, $"\"{Truncate(text, 60)}\" ");
        }
        if (input.X is { } x && input.Y is { } y)
        {
            builder.Append(CultureInfo.InvariantCulture, $"({x:0.#},{y:0.#})");
            if (input.ToX is { } toX && input.ToY is { } toY)
            {
                builder.Append(CultureInfo.InvariantCulture, $" → ({toX:0.#},{toY:0.#})");
            }
            builder.Append(' ');
        }
        if (input.Dx is { } dx && input.Dy is { } dy)
        {
            builder.Append(CultureInfo.InvariantCulture, $"Δ({dx:0.#},{dy:0.#}) ");
        }
        if (input.Button is { } button)
        {
            builder.Append(button).Append(' ');
        }
        if (input.Count is > 1)
        {
            builder.Append(CultureInfo.InvariantCulture, $"×{input.Count}");
        }
    }

    private static void AppendResult(StringBuilder builder, LogEntry entry)
    {
        var result = entry.Result;
        if (result is null)
        {
            return;
        }
        builder.Append(result.Status == "ok" ? "→ ok" : $"→ {result.Error?.Code ?? result.Status}");
        builder.Append(CultureInfo.InvariantCulture, $" {result.Ms:0}ms");
        if (result.Frames is { } frames)
        {
            builder.Append(CultureInfo.InvariantCulture, $" {frames}f");
        }
        if (result.Summary is { } summary)
        {
            builder.Append("  ").Append(summary);
        }
        else if (result.Error is { } error)
        {
            builder.Append("  ").Append(error.Message);
        }
    }

    private static string Truncate(string text, int length)
    {
        var line = text.ReplaceLineEndings(" ");
        return line.Length <= length ? line : line[..(length - 1)] + "…";
    }
}
