using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// A response from a running application instance to a CLI command: written as
/// <c>responses/cmd-&lt;id&gt;.json</c> by the file transport, or sent as one line by the socket transport.
/// </summary>
public sealed class AgentResponse
{
    /// <summary>The message type on the socket transport, <c>"res"</c>; omitted by the file transport.</summary>
    public string? Type { get; set; }

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

    /// <summary>The receiver's frame number when it answered, if it counts frames.</summary>
    public long? Frame { get; set; }

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
        Result = MessageElement(message),
        Timestamp = DateTime.UtcNow.ToString("o"),
    };

    /// <summary>Creates an error response.</summary>
    public static AgentResponse Err(string id, string code, string message, JsonElement? details = null) => new()
    {
        Id = id,
        Status = "error",
        Error = new AgentError { Code = code, Message = message, Details = details },
        Timestamp = DateTime.UtcNow.ToString("o"),
    };

    // Written by hand rather than through a serializer, which would need reflection under AOT.
    private static JsonElement MessageElement(string message)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("message", message);
            writer.WriteEndObject();
        }
        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
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

    /// <summary>
    /// What the receiver knew when it failed, for diagnosis: for a UI action, say, why the app wasn't
    /// idle, the nodes a selector nearly matched, or what covered the node it meant to tap.
    /// </summary>
    public JsonElement? Details { get; set; }
}
