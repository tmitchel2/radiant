using System.Collections.Concurrent;
using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// App-side command queue. Watches the instance's commands directory for new
/// command files and provides them for processing each frame.
/// </summary>
public sealed class CommandReceiver : IDisposable
{
    private readonly string _commandsDir;
    private readonly string _responsesDir;
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentQueue<string> _pendingFiles = new();
    private bool _disposed;

    /// <summary>
    /// Creates a command queue for the named instance.
    /// </summary>
    public CommandReceiver(string instanceName)
    {
        _commandsDir = InstanceRegistry.GetCommandsDir(instanceName);
        _responsesDir = InstanceRegistry.GetResponsesDir(instanceName);

        Directory.CreateDirectory(_commandsDir);
        Directory.CreateDirectory(_responsesDir);

        _watcher = new FileSystemWatcher(_commandsDir, "cmd-*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        _watcher.Created += (_, e) => _pendingFiles.Enqueue(e.FullPath);
        _watcher.Changed += (_, e) => _pendingFiles.Enqueue(e.FullPath);
        _watcher.Renamed += (_, e) => _pendingFiles.Enqueue(e.FullPath);

        // Process any commands already in the directory
        foreach (var file in Directory.GetFiles(_commandsDir, "cmd-*.json"))
        {
            _pendingFiles.Enqueue(file);
        }
    }

    /// <summary>
    /// Drains all pending commands. Call once per frame from the render thread.
    /// Returns parsed commands in filesystem order.
    /// </summary>
    public List<AgentCommand> DrainPendingCommands()
    {
        var commands = new List<AgentCommand>();
        var processed = new HashSet<string>(StringComparer.Ordinal);

        while (_pendingFiles.TryDequeue(out var filePath))
        {
            if (!processed.Add(filePath))
                continue;

            if (!File.Exists(filePath))
                continue;

            try
            {
                var json = File.ReadAllText(filePath);
                var cmd = JsonSerializer.Deserialize(json, AgentJsonContext.Default.AgentCommand);
                if (cmd != null)
                {
                    commands.Add(cmd);
                    // Delete processed command file
                    File.Delete(filePath);
                }
            }
#pragma warning disable CA1031 // Command file may be partially written
            catch
            {
                // Skip — will retry next frame if still in queue
            }
#pragma warning restore CA1031
        }

        return commands;
    }

    /// <summary>
    /// Writes a response file for a processed command.
    /// </summary>
    public void WriteResponse(AgentResponse response)
    {
        var json = JsonSerializer.Serialize(response, AgentJsonContext.Default.AgentResponse);
        var path = Path.Combine(_responsesDir, $"cmd-{response.Id}.json");
        try
        {
            WriteAtomically(path, json);
        }
#pragma warning disable CA1031 // A best-effort IPC response write must never crash the app's main loop.
        catch (Exception ex)
        {
            // Losing a response is recoverable (the caller times out and can retry) — e.g. a File.Move
            // race when two processes briefly share an instance dir, or a transient FS error. Crashing
            // the host/renderer render loop over it is not. Log and continue.
            Console.Error.WriteLine($"[CommandReceiver] Failed to write response {response.Id}: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Block until every written response file has been consumed by the client, or
    /// <paramref name="timeoutMs"/> elapses. The client deletes each response after reading it (that
    /// deletion is the delivery ack), so this returns as soon as the responses directory has no
    /// <c>cmd-*.json</c> left. Call on graceful shutdown so tearing down the instance directory doesn't
    /// race the client's read of the final response (e.g. the <c>app.exit</c> reply). Bounded so a
    /// client that already gave up never stalls shutdown.
    /// </summary>
    public void WaitForResponsesDelivered(int timeoutMs)
    {
        const int StepMs = 15;
        for (var elapsed = 0; elapsed < timeoutMs; elapsed += StepMs)
        {
            if (!Directory.Exists(_responsesDir) || Directory.GetFiles(_responsesDir, "cmd-*.json").Length == 0)
                return;
            Thread.Sleep(StepMs);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
    }

    private static void WriteAtomically(string path, string content)
    {
        var dir = Path.GetDirectoryName(path)!;
        var tmp = Path.Combine(dir, $".{Path.GetFileName(path)}.tmp");
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }
}
