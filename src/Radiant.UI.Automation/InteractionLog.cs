using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Core;

namespace Radiant.UI.Automation;

/// <summary>How an <see cref="InteractionLog"/> records.</summary>
public sealed record InteractionLogOptions
{
    /// <summary>The JSON Lines file to append to; null keeps the log in memory only.</summary>
    public string? Path { get; init; }

    /// <summary>Records what the text typed was; otherwise it's written as <c>•••</c>.</summary>
    public bool RecordText { get; init; } = true;

    /// <summary>Records the pointer moving onto a different element, not only presses.</summary>
    public bool RecordHover { get; init; }

    /// <summary>Records queries (<c>ui.tree</c>, <c>ui.inspect</c>, …) as well as actions.</summary>
    public bool RecordQueries { get; init; }

    /// <summary>How many entries to keep in memory, for <c>log.tail</c> and subscriptions that start in the past.</summary>
    public int Keep { get; init; } = 2000;

    /// <summary>Starts a new file once the log reaches this many bytes.</summary>
    public long RotateBytes { get; init; } = 20 * 1024 * 1024;
}

/// <summary>
/// A record of everything done to a UI app: what people did through the window (presses, typing,
/// scrolling, keys), what agents and tests did through the automation (each action and its result),
/// and what changed (focus). Kept as <see cref="LogEntry"/> lines in memory, optionally appended to a
/// JSON Lines file, and pushed to subscribers. On the UI thread.
/// </summary>
public sealed class InteractionLog : IDisposable
{
    private const float TapSlop = 4f;
    private const double WheelGapSeconds = 0.2;
    private const double TextGapSeconds = 1.0;

    private readonly InteractionLogOptions _options;
    private readonly LinkedList<LogEntry> _recent = new();
    private readonly List<Subscription> _subscriptions = [];
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private StreamWriter? _file;
    private long _written;
    private int _part;
    private long _seq;
    private int _nextSubscription;
    private UIAutomation? _automation;

    // Human input being gathered into one entry.
    private (Vector2 At, int Target, KeyModifiers Modifiers, PointerButton Button)? _down;
    private (Vector2 At, Vector2 Delta, int Target, double Time)? _wheel;
    private (string Text, int Target, double Time)? _typing;
    private int _hovered;
    private int _focused;

    // Who gave the last input, so what it changed (focus) is put down to them; and who's acting now.
    private string _lastSource = LogSources.App;
    private string _acting = LogSources.Agent;

    /// <summary>A log recording as <paramref name="options"/> say.</summary>
    public InteractionLog(InteractionLogOptions? options = null)
    {
        _options = options ?? new InteractionLogOptions();
        if (_options.Path is { } path)
        {
            OpenFile(path);
        }
    }

    /// <summary>The file it writes, if any.</summary>
    public string? Path => _options.Path is null ? null : PartPath(_part);

    /// <summary>Where screenshots it references go: <c>shots</c> beside the file, or in the temp directory.</summary>
    public string ShotsDirectory => _options.Path is { } path
        ? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!, System.IO.Path.GetFileNameWithoutExtension(path) + "-shots")
        : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "radiant-shots-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

    /// <summary>The latest sequence number.</summary>
    public long Seq => _seq;

    /// <summary>Raised with each entry as it's written.</summary>
    public event Action<LogEntry>? Written;

    /// <summary>The last <paramref name="count"/> entries, oldest first.</summary>
    public IReadOnlyList<LogEntry> Tail(int count = 50) => [.. _recent.Skip(Math.Max(0, _recent.Count - count))];

    /// <summary>Stamps and writes an entry: its sequence number, time and frame are filled in.</summary>
    public LogEntry Write(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entry.Seq = ++_seq;
        entry.T = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        entry.Ms = Math.Round(_clock.Elapsed.TotalMilliseconds, 1);
        entry.Frame = _automation?.Session.Frame ?? 0;
        _recent.AddLast(entry);
        while (_recent.Count > _options.Keep)
        {
            _recent.RemoveFirst();
        }
        if (_file is not null)
        {
            var line = LogFormatter.ToJsonLine(entry);
            _file.WriteLine(line);
            _file.Flush();
            _written += line.Length + 1;
            if (_written > _options.RotateBytes)
            {
                _file.Dispose();
                _part++;
                OpenFile(PartPath(_part));
            }
        }
        foreach (var subscription in _subscriptions.ToArray())
        {
            subscription.Offer(entry);
        }
        Written?.Invoke(entry);
        return entry;
    }

    /// <summary>A note from whoever's watching.</summary>
    public LogEntry Note(string text, string source = LogSources.Agent) =>
        Write(new LogEntry { Kind = LogKinds.Note, Src = source, Note = text });

    /// <summary>
    /// Pushes entries to <paramref name="connection"/> as <c>log</c> events: those after
    /// <paramref name="since"/> still in memory first (none for -1), then each as it's written.
    /// </summary>
    public string Subscribe(IAgentConnection connection, long since, IReadOnlySet<string>? sources, IReadOnlySet<string>? kinds)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var subscription = new Subscription("s" + (++_nextSubscription).ToString(CultureInfo.InvariantCulture), connection, sources, kinds);
        _subscriptions.Add(subscription);
        if (since >= 0)
        {
            foreach (var entry in _recent.Where(e => e.Seq > since))
            {
                subscription.Offer(entry);
            }
        }
        return subscription.Id;
    }

    /// <summary>Stops a subscription; false if there's no such one.</summary>
    public bool Unsubscribe(string id) => _subscriptions.RemoveAll(s => s.Id == id) > 0;

    /// <summary>Stops every subscription of a connection that has gone.</summary>
    public void Unsubscribe(IAgentConnection connection) => _subscriptions.RemoveAll(s => ReferenceEquals(s.Connection, connection));

    /// <summary>Starts recording an automation's app: its people's input, its actions, its changes of focus.</summary>
    internal void Attach(UIAutomation automation)
    {
        _automation = automation;
        automation.Session.Root.InputReceived += OnInput;
        automation.Session.AfterUpdate += OnFrame;
        automation.Dispatcher.Started += OnStarted;
        automation.Dispatcher.Completed += OnCompleted;
        Write(new LogEntry { Kind = LogKinds.Session, Src = LogSources.App, Note = $"started {automation.AppName ?? "app"} ({(automation.Session.IsHeadless ? "headless" : "window")}, {automation.Session.ClockMode.ToString().ToLowerInvariant()} clock)" });
    }

    internal void Detach()
    {
        if (_automation is not { } automation)
        {
            return;
        }
        Flush();
        automation.Session.Root.InputReceived -= OnInput;
        automation.Session.AfterUpdate -= OnFrame;
        automation.Dispatcher.Started -= OnStarted;
        automation.Dispatcher.Completed -= OnCompleted;
        Write(new LogEntry { Kind = LogKinds.Session, Src = LogSources.App, Note = "ended" });
        _automation = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Detach();
        _file?.Dispose();
        _file = null;
    }

    private void OpenFile(string path)
    {
        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        _file = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = false };
        _written = _file.BaseStream.Length;
    }

    private string PartPath(int part) => part == 0
        ? _options.Path!
        : System.IO.Path.ChangeExtension(_options.Path!, part.ToString(CultureInfo.InvariantCulture) + System.IO.Path.GetExtension(_options.Path));

    // ------------------------------------------------------------------ agents' and tests' actions

    private void OnStarted(AgentCallContext call)
    {
        if (!Records(call.Command.Action))
        {
            return;
        }
        Flush();
        _acting = SourceOf(call.Connection);
        Write(new LogEntry
        {
            Kind = LogKinds.Action,
            Src = _acting,
            Action = new LogAction
            {
                Id = call.Command.Id,
                Name = call.Command.Action,
                Selector = SelectorOf(call.Params),
                Client = call.Connection.Client,
                Params = _options.RecordText ? call.Command.Params : WithoutText(call.Command.Params),
            },
        });
    }

    private void OnCompleted(AgentCallContext call, AgentResponse response)
    {
        if (!Records(call.Command.Action))
        {
            return;
        }
        Flush();
        Write(new LogEntry
        {
            Kind = LogKinds.Result,
            Src = SourceOf(call.Connection),
            Action = new LogAction { Id = call.Command.Id, Name = call.Command.Action, Selector = SelectorOf(call.Params), Client = call.Connection.Client },
            Result = new LogResult
            {
                Status = response.Status,
                Ms = Math.Round(response.DurationMs, 1),
                Frames = response.Frame is { } frame ? frame - call.StartFrame : null,
                Summary = Summarize(response),
                Error = response.Error,
            },
            Target = TargetOf(response),
            Screenshot = response.Status == "ok" && call.Command.Action == "ui.screenshot" && response.Result is { } shot && shot.TryGetProperty("path", out var path) ? path.GetString() : null,
        });
    }

    // The params with any text to type blanked, when typed text isn't recorded.
    private static JsonElement? WithoutText(JsonElement? parameters)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } p || !p.TryGetProperty("text", out _))
        {
            return parameters;
        }
        return Json.Write(writer =>
        {
            writer.WriteStartObject();
            foreach (var property in p.EnumerateObject())
            {
                if (property.Name == "text")
                {
                    writer.WriteString("text", "•••");
                }
                else
                {
                    property.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        });
    }

    private bool Records(string action) =>
        _options.RecordQueries || !(action.StartsWith("log.", StringComparison.Ordinal) || action is "ui.tree" or "ui.inspect" or "ui.query" or "app.info" or "actions.list");

    private static string SourceOf(IAgentConnection connection) => connection.Client == "in-process" ? LogSources.Test : LogSources.Agent;

    private static string? SelectorOf(JsonElement parameters)
    {
        foreach (var name in new[] { "selector", "target" })
        {
            if (parameters.TryGetProperty(name, out var value))
            {
                try
                {
                    return value.Deserialize(AgentJsonContext.Default.Selector)?.ToString();
                }
                catch (JsonException)
                {
                    return value.ToString();
                }
            }
        }
        return null;
    }

    private static LogTarget? TargetOf(AgentResponse response)
    {
        if (response.Result is not { ValueKind: JsonValueKind.Object } result || !result.TryGetProperty("target", out var target) || target.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return new LogTarget
        {
            Id = target.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
            Role = target.TryGetProperty("role", out var role) ? role.GetString() : null,
            Label = target.TryGetProperty("label", out var label) ? label.GetString() : null,
            TestId = target.TryGetProperty("testId", out var testId) ? testId.GetString() : null,
        };
    }

    private static string? Summarize(AgentResponse response)
    {
        if (response.Status != "ok" || response.Result is not { ValueKind: JsonValueKind.Object } result)
        {
            return null;
        }
        var parts = new List<string>();
        if (result.TryGetProperty("at", out var at))
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"at ({at.GetProperty("x").GetSingle():0.#},{at.GetProperty("y").GetSingle():0.#})"));
        }
        if (result.TryGetProperty("value", out var value))
        {
            parts.Add($"value \"{value.GetString()}\"");
        }
        if (result.TryGetProperty("path", out var path))
        {
            parts.Add(path.GetString() ?? "");
        }
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    // The automation is about to give input (or press or focus directly): what follows is the actor's doing.
    internal void ActingNow() => _lastSource = _acting;

    // ------------------------------------------------------------------ people's input

    private void OnInput(UIInputEvent input)
    {
        if (_automation is not { } automation)
        {
            return;
        }
        if (automation.IsSynthesizing)
        {
            _lastSource = _acting;
            return;
        }
        if (input.Type != UIInputType.PointerMove)
        {
            _lastSource = LogSources.Human;
        }
        var now = _clock.Elapsed.TotalSeconds;
        switch (input.Type)
        {
            case UIInputType.PointerMove:
                if (_options.RecordHover && _down is null && Semantic(input.TargetId) is { } over && over.Id != _hovered)
                {
                    _hovered = over.Id;
                    Human(new LogInput { Type = "hover", X = input.Position.X, Y = input.Position.Y }, over.Id);
                }
                break;
            case UIInputType.PointerDown:
                Flush();
                _down = (input.Position, input.TargetId, input.Modifiers, input.Button);
                break;
            case UIInputType.PointerUp when _down is { } down:
                _down = null;
                var moved = Vector2.Distance(down.At, input.Position) > TapSlop;
                Human(new LogInput
                {
                    Type = moved ? "drag" : "tap",
                    X = down.At.X,
                    Y = down.At.Y,
                    ToX = moved ? input.Position.X : null,
                    ToY = moved ? input.Position.Y : null,
                    Button = down.Button == PointerButton.Left ? null : down.Button.ToString().ToLowerInvariant(),
                    Mods = Mods(down.Modifiers),
                }, down.Target);
                break;
            case UIInputType.Wheel:
                if (_wheel is { } wheel && wheel.Target == input.TargetId && now - wheel.Time < WheelGapSeconds)
                {
                    _wheel = (wheel.At, wheel.Delta + input.Delta, wheel.Target, now);
                }
                else
                {
                    Flush();
                    _wheel = (input.Position, input.Delta, input.TargetId, now);
                }
                break;
            case UIInputType.Text:
                if (_typing is { } typing && typing.Target == input.TargetId && now - typing.Time < TextGapSeconds)
                {
                    _typing = (typing.Text + input.Text, typing.Target, now);
                }
                else
                {
                    Flush();
                    _typing = (input.Text ?? "", input.TargetId, now);
                }
                break;
            case UIInputType.KeyDown when !input.IsRepeat && !IsModifier(input.Key):
                // A printable key typed as text is recorded with the text; the rest as keys.
                if (input.Modifiers is KeyModifiers.None or KeyModifiers.Shift && input.Key is >= KeyCode.Space and <= KeyCode.GraveAccent)
                {
                    break;
                }
                Flush();
                Human(new LogInput { Type = "key", Key = new KeyChord(input.Key, input.Modifiers).ToInvariantString() }, input.TargetId);
                break;
            case UIInputType.FileDrop:
                Flush();
                Human(new LogInput { Type = "drop", X = input.Position.X, Y = input.Position.Y, Text = string.Join(", ", input.Paths ?? []) }, input.TargetId);
                break;
        }
    }

    private void OnFrame()
    {
        var now = _clock.Elapsed.TotalSeconds;
        if (_wheel is { } wheel && now - wheel.Time >= WheelGapSeconds || _typing is { } typing && now - typing.Time >= TextGapSeconds)
        {
            Flush();
        }
        if (_automation is { } automation && automation.Session.Root.FocusedId is var focused && focused != _focused)
        {
            _focused = focused;
            if (focused != 0)
            {
                Write(new LogEntry { Kind = LogKinds.State, Src = _lastSource, State = "focus", Target = Target(focused) });
            }
        }
    }

    // Writes whatever input was being gathered.
    private void Flush()
    {
        if (_wheel is { } wheel)
        {
            _wheel = null;
            Human(new LogInput { Type = "wheel", X = wheel.At.X, Y = wheel.At.Y, Dx = wheel.Delta.X, Dy = wheel.Delta.Y }, wheel.Target);
        }
        if (_typing is { } typing)
        {
            _typing = null;
            Human(new LogInput { Type = "text", Text = _options.RecordText ? typing.Text : "•••" }, typing.Target);
        }
    }

    private void Human(LogInput input, int target) =>
        Write(new LogEntry { Kind = LogKinds.Input, Src = LogSources.Human, Input = input, Target = Target(target) });

    private LogTarget? Target(int id)
    {
        if (_automation is null || id == 0 || Semantic(id) is not { } match)
        {
            return null;
        }
        var engine = new SelectorEngine(_automation.Session.Root);
        return new LogTarget { Id = match.Id, Role = match.Role, Label = match.Label, TestId = match.TestId, Path = engine.PathOf(match.Id) };
    }

    // The element a node belongs to: the nearest node at or above it that assistive technology sees.
    private Match? Semantic(int id)
    {
        if (_automation is null || _automation.Session.Root.FindNode(id) is not { } node)
        {
            return null;
        }
        return new SelectorEngine(_automation.Session.Root).NearestSemantic(node);
    }

    private static bool IsModifier(KeyCode key) => key is >= KeyCode.ShiftLeft and <= KeyCode.SuperRight or KeyCode.CapsLock;

    private static string[]? Mods(KeyModifiers modifiers)
    {
        if (modifiers == KeyModifiers.None)
        {
            return null;
        }
        var mods = new List<string>();
        if ((modifiers & KeyModifiers.Control) != 0)
        {
            mods.Add("ctrl");
        }
        if ((modifiers & KeyModifiers.Alt) != 0)
        {
            mods.Add("alt");
        }
        if ((modifiers & KeyModifiers.Shift) != 0)
        {
            mods.Add("shift");
        }
        if ((modifiers & KeyModifiers.Super) != 0)
        {
            mods.Add("cmd");
        }
        return [.. mods];
    }

    private sealed class Subscription(string id, IAgentConnection connection, IReadOnlySet<string>? sources, IReadOnlySet<string>? kinds)
    {
        public string Id { get; } = id;

        public IAgentConnection Connection { get; } = connection;

        public void Offer(LogEntry entry)
        {
            if (sources is not null && !sources.Contains(entry.Src) || kinds is not null && !kinds.Contains(entry.Kind))
            {
                return;
            }
            Connection.SendEvent(new AgentEvent
            {
                Event = "log",
                Sub = Id,
                Seq = entry.Seq,
                Data = JsonSerializer.SerializeToElement(entry, AgentJsonContext.Compact.LogEntry),
            });
        }
    }
}
