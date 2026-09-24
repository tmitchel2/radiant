using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// A response from a running application instance to a CLI command.
/// Written as <c>responses/cmd-&lt;id&gt;.json</c>.
/// </summary>
public sealed class AgentResponse
{
    /// <summary>Command ID this response corresponds to.</summary>
    public string Id { get; set; } = "";

    /// <summary>Status: "ok", "error", or "accepted" (for async operations).</summary>
    public string Status { get; set; } = "ok";

    /// <summary>Action-specific result as raw JSON. Null on error.</summary>
    public JsonElement? Result { get; set; }

    /// <summary>Error details when status is "error".</summary>
    public AgentError? Error { get; set; }

    /// <summary>Time taken to process the command in milliseconds.</summary>
    public double DurationMs { get; set; }

    /// <summary>UTC timestamp in ISO 8601 format.</summary>
    public string Timestamp { get; set; } = "";

    /// <summary>Creates a success response with a pre-serialized result.</summary>
    public static AgentResponse Ok(string id, JsonElement? result, double durationMs) => new()
    {
        Id = id,
        Status = "ok",
        Result = result,
        DurationMs = durationMs,
        Timestamp = DateTime.UtcNow.ToString("o"),
    };

    /// <summary>Creates an accepted response for async operations.</summary>
    public static AgentResponse Accepted(string id, string message) => new()
    {
        Id = id,
        Status = "accepted",
        Result = ToElement(new Dictionary<string, string> { ["message"] = message }),
        Timestamp = DateTime.UtcNow.ToString("o"),
    };

    /// <summary>Creates an error response.</summary>
    public static AgentResponse Err(string id, string code, string message) => new()
    {
        Id = id,
        Status = "error",
        Error = new AgentError { Code = code, Message = message },
        Timestamp = DateTime.UtcNow.ToString("o"),
    };

    private static JsonElement ToElement(Dictionary<string, string> dict)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(dict);
        return JsonDocument.Parse(bytes).RootElement.Clone();
    }
}

/// <summary>
/// Error details for a failed command.
/// </summary>
public sealed class AgentError
{
    /// <summary>Error code, e.g. "invalid_params", "not_found", "internal".</summary>
    public string Code { get; set; } = "";

    /// <summary>Human-readable error message.</summary>
    public string Message { get; set; } = "";
}
