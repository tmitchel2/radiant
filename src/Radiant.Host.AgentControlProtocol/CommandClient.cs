using System.Globalization;
using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// CLI-side command client. Writes command files and polls for responses.
/// </summary>
public sealed class CommandClient
{
    private readonly string _commandsDir;
    private readonly string _responsesDir;

    /// <summary>
    /// Creates a command client for the named instance.
    /// </summary>
    public CommandClient(string instanceName)
    {
        _commandsDir = InstanceRegistry.GetCommandsDir(instanceName);
        _responsesDir = InstanceRegistry.GetResponsesDir(instanceName);
    }

    /// <summary>
    /// Sends a command and waits for the response.
    /// </summary>
    /// <param name="action">Action name, e.g. "scene.load".</param>
    /// <param name="paramsJson">Raw JSON params string, or null.</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default 30 seconds).</param>
    /// <param name="commandTimeoutMs">How long the receiver may take, sent with the command; its default if null.</param>
    /// <returns>The response, or an error response on timeout.</returns>
    public AgentResponse Send(string action, string? paramsJson = null, int timeoutMs = 30000, int? commandTimeoutMs = null)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var command = new AgentCommand
        {
            Id = id,
            Action = action,
            Params = paramsJson != null ? JsonDocument.Parse(paramsJson).RootElement : null,
            Timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            TimeoutMs = commandTimeoutMs,
        };

        // Write command file atomically
        var cmdPath = Path.Combine(_commandsDir, $"cmd-{id}.json");
        var json = JsonSerializer.Serialize(command, AgentJsonContext.Default.AgentCommand);
        WriteAtomically(cmdPath, json);

        // Poll for response with exponential backoff
        var responsePath = Path.Combine(_responsesDir, $"cmd-{id}.json");
        var elapsed = 0;
        var delay = 10; // Start at 10ms

        while (elapsed < timeoutMs)
        {
            if (File.Exists(responsePath))
            {
                try
                {
                    var responseJson = File.ReadAllText(responsePath);
                    var response = JsonSerializer.Deserialize(responseJson, AgentJsonContext.Default.AgentResponse);

                    // Clean up response file
                    File.Delete(responsePath);

                    return response ?? AgentResponse.Err(id, "parse_error", "Failed to parse response");
                }
#pragma warning disable CA1031 // Response file may be partially written
                catch
                {
                    // Retry on next poll — file may be partially written
                }
#pragma warning restore CA1031
            }

            Thread.Sleep(delay);
            elapsed += delay;
            delay = Math.Min(delay * 2, 200); // Cap at 200ms
        }

        // Timeout — clean up command file if still there
        if (File.Exists(cmdPath))
            File.Delete(cmdPath);

        return AgentResponse.Err(id, "timeout", $"No response within {timeoutMs}ms");
    }

    private static void WriteAtomically(string path, string content)
    {
        var dir = Path.GetDirectoryName(path)!;
        var tmp = Path.Combine(dir, $".{Path.GetFileName(path)}.tmp");
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }
}
