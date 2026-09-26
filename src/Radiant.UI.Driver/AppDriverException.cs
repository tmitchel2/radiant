using System.Text.Json;
using Radiant.Host.AgentControlProtocol;

namespace Radiant.UI.Driver;

/// <summary>
/// An action or expectation that failed: the app's error code and message, what it knew
/// (<see cref="Details"/>), and the end of the interaction log, so a failed test says what happened.
/// </summary>
public sealed class AppDriverException : Exception
{
    /// <summary>A failure.</summary>
    public AppDriverException(string code, string message, JsonElement? details = null, IReadOnlyList<LogEntry>? recent = null)
        : base(Compose(code, message, recent))
    {
        Code = code;
        Details = details;
        Recent = recent ?? [];
    }

    /// <summary>A failure with no code.</summary>
    public AppDriverException() : this(AgentErrorCodes.Internal, "The app driver failed.")
    {
    }

    /// <summary>A failure with a message.</summary>
    public AppDriverException(string message) : this(AgentErrorCodes.Internal, message)
    {
    }

    /// <summary>A failure with a message and a cause.</summary>
    public AppDriverException(string message, Exception innerException) : base(message, innerException)
    {
        Code = AgentErrorCodes.Internal;
        Recent = [];
    }

    /// <summary>The <see cref="AgentErrorCodes">error code</see>.</summary>
    public string Code { get; }

    /// <summary>What the app knew: near matches, what covered the element, why it was busy.</summary>
    public JsonElement? Details { get; }

    /// <summary>The end of the interaction log when it failed.</summary>
    public IReadOnlyList<LogEntry> Recent { get; }

    private static string Compose(string code, string message, IReadOnlyList<LogEntry>? recent)
    {
        if (recent is not { Count: > 0 })
        {
            return $"[{code}] {message}";
        }
        return $"[{code}] {message}\n\nWhat happened last:\n  {string.Join("\n  ", recent.Select(LogFormatter.Format))}";
    }
}
