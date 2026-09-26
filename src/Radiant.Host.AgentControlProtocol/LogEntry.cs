using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// One line of an interaction log (JSON Lines): an input from a person or an agent, an action and its
/// result, a change of state, a screenshot or a note. See <see cref="LogFormatter"/> for the readable form.
/// </summary>
public sealed class LogEntry
{
    /// <summary>Its sequence number in the log, from 1.</summary>
    public long Seq { get; set; }

    /// <summary>When, as UTC ISO 8601.</summary>
    public string T { get; set; } = "";

    /// <summary>Milliseconds since the log began.</summary>
    public double Ms { get; set; }

    /// <summary>The app's frame number.</summary>
    public long Frame { get; set; }

    /// <summary>Who: <see cref="LogSources"/>.</summary>
    public string Src { get; set; } = LogSources.App;

    /// <summary>What: <see cref="LogKinds"/>.</summary>
    public string Kind { get; set; } = LogKinds.Note;

    /// <summary>The input, for <see cref="LogKinds.Input"/>.</summary>
    public LogInput? Input { get; set; }

    /// <summary>The element the input or action reached, or the state is about.</summary>
    public LogTarget? Target { get; set; }

    /// <summary>The action, for <see cref="LogKinds.Action"/> and <see cref="LogKinds.Result"/>.</summary>
    public LogAction? Action { get; set; }

    /// <summary>The action's outcome, for <see cref="LogKinds.Result"/>.</summary>
    public LogResult? Result { get; set; }

    /// <summary>The change, for <see cref="LogKinds.State"/>: <c>focus</c>, <c>dialog.open</c>, <c>dialog.close</c>.</summary>
    public string? State { get; set; }

    /// <summary>A screenshot's path, relative to the log's directory.</summary>
    public string? Screenshot { get; set; }

    /// <summary>Free text: a note, or a session's start or end.</summary>
    public string? Note { get; set; }
}

/// <summary>An input event in the log. Coordinates are logical window points.</summary>
public sealed class LogInput
{
    /// <summary><c>tap</c>, <c>drag</c>, <c>pointerDown</c>, <c>pointerUp</c>, <c>hover</c>, <c>wheel</c>, <c>key</c>, <c>text</c>, <c>drop</c>.</summary>
    public string Type { get; set; } = "";

    /// <summary>Where, for a pointer.</summary>
    public float? X { get; set; }

    /// <summary>Where, for a pointer.</summary>
    public float? Y { get; set; }

    /// <summary>Where a drag ended.</summary>
    public float? ToX { get; set; }

    /// <summary>Where a drag ended.</summary>
    public float? ToY { get; set; }

    /// <summary>A wheel's travel, in pixels.</summary>
    public float? Dx { get; set; }

    /// <summary>A wheel's travel, in pixels.</summary>
    public float? Dy { get; set; }

    /// <summary>The pointer button, if not the left.</summary>
    public string? Button { get; set; }

    /// <summary>A tap's click count, if more than one.</summary>
    public int? Count { get; set; }

    /// <summary>The modifier keys held: <c>shift</c>, <c>ctrl</c>, <c>alt</c>, <c>cmd</c>.</summary>
    public string[]? Mods { get; set; }

    /// <summary>The key, for <c>key</c>.</summary>
    public string? Key { get; set; }

    /// <summary>The text, for <c>text</c>; <c>"•••"</c> when redacted.</summary>
    public string? Text { get; set; }
}

/// <summary>The element something happened to.</summary>
public sealed class LogTarget
{
    /// <summary>Its node id.</summary>
    public int Id { get; set; }

    /// <summary>Its semantics role.</summary>
    public string? Role { get; set; }

    /// <summary>Its label.</summary>
    public string? Label { get; set; }

    /// <summary>Its test ID.</summary>
    public string? TestId { get; set; }

    /// <summary>Where it is: its labelled ancestors, outermost first, e.g. <c>dialog "Edit" &gt; button "Save"</c>.</summary>
    public string? Path { get; set; }
}

/// <summary>An agent action in the log.</summary>
public sealed class LogAction
{
    /// <summary>The command's id.</summary>
    public string Id { get; set; } = "";

    /// <summary>The action, e.g. <c>ui.tap</c>.</summary>
    public string Name { get; set; } = "";

    /// <summary>The selector it was given, in the compact syntax.</summary>
    public string? Selector { get; set; }

    /// <summary>Who sent it, e.g. <c>socket#3</c> or <c>file</c>.</summary>
    public string? Client { get; set; }

    /// <summary>Its params.</summary>
    public JsonElement? Params { get; set; }
}

/// <summary>An action's outcome in the log.</summary>
public sealed class LogResult
{
    /// <summary><c>ok</c> or <c>error</c>.</summary>
    public string Status { get; set; } = "ok";

    /// <summary>How long it took, in milliseconds.</summary>
    public double Ms { get; set; }

    /// <summary>How many frames it took.</summary>
    public long? Frames { get; set; }

    /// <summary>One line on what it did.</summary>
    public string? Summary { get; set; }

    /// <summary>The error, if it failed.</summary>
    public AgentError? Error { get; set; }
}

/// <summary>The <see cref="LogEntry.Src"/> values.</summary>
public static class LogSources
{
    /// <summary>A person, through the window.</summary>
    public const string Human = "human";

    /// <summary>An agent, through a transport.</summary>
    public const string Agent = "agent";

    /// <summary>A test, through the in-process driver.</summary>
    public const string Test = "test";

    /// <summary>The app itself.</summary>
    public const string App = "app";
}

/// <summary>The <see cref="LogEntry.Kind"/> values.</summary>
public static class LogKinds
{
    /// <summary>An input event.</summary>
    public const string Input = "input";

    /// <summary>An action starting.</summary>
    public const string Action = "action";

    /// <summary>An action finishing.</summary>
    public const string Result = "result";

    /// <summary>A change of state: focus, a dialog.</summary>
    public const string State = "state";

    /// <summary>A screenshot taken.</summary>
    public const string Screenshot = "screenshot";

    /// <summary>A note.</summary>
    public const string Note = "note";

    /// <summary>A session starting or ending.</summary>
    public const string Session = "session";
}
