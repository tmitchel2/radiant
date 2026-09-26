using System.Globalization;

namespace Radiant.AgentCli;

/// <summary>A command line split into positionals and options: <c>--name value</c>, <c>--flag</c>, <c>--name=value</c>, and everything after <c>--</c>.</summary>
internal sealed class Args
{
    // Options that take no value.
    private static readonly HashSet<string> s_flags = new(StringComparer.Ordinal)
    {
        "json", "q", "quiet", "all", "render", "headless", "right", "force", "no-scroll", "replace", "submit", "wheel", "px",
        "visible", "gone", "hidden", "enabled", "disabled", "focused", "checked", "unchecked", "hittable", "exists", "f", "follow",
        "no-wait", "help", "h", "annotate",
    };

    private readonly Dictionary<string, string?> _options = new(StringComparer.Ordinal);

    public Args(IEnumerable<string> args)
    {
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var arg = list[i];
            if (arg == "--")
            {
                Rest.AddRange(list.Skip(i + 1));
                break;
            }
            if (arg.Length > 1 && arg[0] == '-' && !IsNumber(arg))
            {
                var name = arg.TrimStart('-');
                string? value = null;
                var eq = name.IndexOf('=', StringComparison.Ordinal);
                if (eq >= 0)
                {
                    value = name[(eq + 1)..];
                    name = name[..eq];
                }
                else if (!s_flags.Contains(name) && i + 1 < list.Count)
                {
                    value = list[++i];
                }
                _options[Alias(name)] = value;
                continue;
            }
            Positionals.Add(arg);
        }
    }

    public List<string> Positionals { get; } = [];

    public List<string> Rest { get; } = [];

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Get(string name) => _options.GetValueOrDefault(name);

    public int? Int(string name) => Get(name) is { } value
        ? int.TryParse(value, CultureInfo.InvariantCulture, out var number) ? number : throw new UsageException($"--{name} takes a whole number, not '{value}'.")
        : null;

    public float? Float(string name) => Get(name) is { } value
        ? float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : throw new UsageException($"--{name} takes a number, not '{value}'.")
        : null;

    /// <summary>A pair <c>x,y</c>.</summary>
    public (float X, float Y)? Pair(string name)
    {
        if (Get(name) is not { } value)
        {
            return null;
        }
        var parts = value.Split(',', 'x');
        return parts.Length == 2
            && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            ? (x, y)
            : throw new UsageException($"--{name} takes x,y, not '{value}'.");
    }

    /// <summary>A duration: <c>500ms</c>, <c>10s</c>, <c>2m</c>, or plain seconds.</summary>
    public TimeSpan? Duration(string name)
    {
        if (Get(name) is not { } value)
        {
            return null;
        }
        var (number, unit) = value.EndsWith("ms", StringComparison.Ordinal) ? (value[..^2], 0.001)
            : value.EndsWith('s') ? (value[..^1], 1.0)
            : value.EndsWith('m') ? (value[..^1], 60.0)
            : (value, 1.0);
        return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)
            ? TimeSpan.FromSeconds(amount * unit)
            : throw new UsageException($"--{name} takes a duration such as 500ms or 10s, not '{value}'.");
    }

    public string Positional(int index, string what) =>
        index < Positionals.Count ? Positionals[index] : throw new UsageException($"Missing {what}.");

    private static string Alias(string name) => name switch
    {
        "i" => "instance",
        "q" => "quiet",
        "f" => "follow",
        "h" => "help",
        _ => name,
    };

    private static bool IsNumber(string arg) => double.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
}

/// <summary>A command line that doesn't make sense: exit code 64.</summary>
internal sealed class UsageException(string message) : Exception(message)
{
    public UsageException() : this("Bad usage.")
    {
    }

    public UsageException(string message, Exception inner) : this(message + " " + inner.Message)
    {
    }
}
