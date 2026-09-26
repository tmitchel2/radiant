using System.Diagnostics;
using System.Text.Json;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>
/// Manages instance registration under <see cref="RootDir"/> (<c>&lt;data directory&gt;/instances/</c>).
/// Provides discovery, registration, deregistration, and PID liveness checking.
/// </summary>
public static class InstanceRegistry
{
    /// <summary>
    /// Root directory for all instance registrations. Every process of one application must agree on it;
    /// an application sets it through <c>RadiantAppIdentity.Use</c>, or directly when it does not reference
    /// the host. Defaults to <c>$RADIANT_INSTANCES_DIR</c> if that's set (to keep tests apart), else
    /// <c>~/.radiant/instances</c>.
    /// </summary>
    public static string RootDir { get; set; } =
        Environment.GetEnvironmentVariable("RADIANT_INSTANCES_DIR") is { Length: > 0 } fromEnvironment
            ? fromEnvironment
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".radiant", "instances");

    /// <summary>
    /// Where interaction logs go: <c>logs</c> beside <see cref="RootDir"/>, since an instance's own
    /// directory is deleted when it deregisters.
    /// </summary>
    public static string LogsDir =>
        Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(RootDir)) ?? RootDir, "logs");

    /// <summary>
    /// Registers a new instance. Creates the instance directory, writes instance.json,
    /// and creates commands/ and responses/ subdirectories.
    /// </summary>
    public static void Register(InstanceInfo info)
    {
        var dir = Path.Combine(RootDir, info.Name);
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "commands"));
        Directory.CreateDirectory(Path.Combine(dir, "responses"));

        var json = JsonSerializer.Serialize(info, AgentJsonContext.Default.InstanceInfo);
        WriteAtomically(Path.Combine(dir, "instance.json"), json);

        // Create symlink to state directory if it exists
        var stateLink = Path.Combine(dir, "state");
        if (!string.IsNullOrEmpty(info.StateDirectory) && Directory.Exists(info.StateDirectory) && !Path.Exists(stateLink))
        {
            try
            {
                Directory.CreateSymbolicLink(stateLink, info.StateDirectory);
            }
#pragma warning disable CA1031 // Symlink creation may fail on some platforms
            catch
            {
                // Fall through — state link is optional
            }
#pragma warning restore CA1031
        }
    }

    /// <summary>
    /// Deregisters an instance by removing its directory.
    /// </summary>
    public static void Deregister(string name)
    {
        var dir = Path.Combine(RootDir, name);
        if (Directory.Exists(dir))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
#pragma warning disable CA1031 // Directory may be locked
            catch
            {
                // Best-effort cleanup
            }
#pragma warning restore CA1031
        }
    }

    /// <summary>
    /// Lists all live instances. Dead instances (stale PIDs) are cleaned up automatically.
    /// </summary>
    public static InstanceInfo[] ListInstances()
    {
        if (!Directory.Exists(RootDir))
            return [];

        var results = new List<InstanceInfo>();

        foreach (var dir in Directory.GetDirectories(RootDir))
        {
            var infoPath = Path.Combine(dir, "instance.json");
            if (!File.Exists(infoPath))
                continue;

            try
            {
                var json = File.ReadAllText(infoPath);
                var info = JsonSerializer.Deserialize(json, AgentJsonContext.Default.InstanceInfo);
                if (info == null)
                    continue;

                if (IsAlive(info.Pid))
                {
                    results.Add(info);
                }
                else
                {
                    // Clean up stale instance
                    Deregister(info.Name);
                }
            }
#pragma warning disable CA1031 // File may be partially written or locked
            catch
            {
                // Skip unreadable entries
            }
#pragma warning restore CA1031
        }

        return [.. results];
    }

    /// <summary>
    /// Gets info for a specific named instance. Returns null if not found or dead.
    /// </summary>
    public static InstanceInfo? GetInstance(string name)
    {
        var infoPath = Path.Combine(RootDir, name, "instance.json");
        if (!File.Exists(infoPath))
            return null;

        try
        {
            var json = File.ReadAllText(infoPath);
            var info = JsonSerializer.Deserialize(json, AgentJsonContext.Default.InstanceInfo);
            if (info == null)
                return null;

            if (!IsAlive(info.Pid))
            {
                Deregister(name);
                return null;
            }

            return info;
        }
#pragma warning disable CA1031 // File may be partially written or locked
        catch
        {
            return null;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Gets the commands directory for a named instance.
    /// </summary>
    public static string GetCommandsDir(string name) =>
        Path.Combine(RootDir, name, "commands");

    /// <summary>
    /// Gets the responses directory for a named instance.
    /// </summary>
    public static string GetResponsesDir(string name) =>
        Path.Combine(RootDir, name, "responses");

    /// <summary>Gets the directory of a named instance.</summary>
    public static string GetInstanceDir(string name) => Path.Combine(RootDir, name);

    /// <summary>
    /// Gets the socket a named instance's socket transport listens on: <c>agent.sock</c> in its directory,
    /// or, where that path is too long for a Unix socket (about 104 bytes), a name in the temp directory
    /// derived from it.
    /// </summary>
    public static string GetSocketPath(string name)
    {
        var path = Path.Combine(RootDir, name, "agent.sock");
        if (System.Text.Encoding.UTF8.GetByteCount(path) <= 100)
        {
            return path;
        }
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path)))[..16];
        return Path.Combine(Path.GetTempPath(), $"radiant-{hash.ToLowerInvariant()}.sock");
    }

    /// <summary>
    /// Checks if a process with the given PID is still running.
    /// </summary>
    public static bool IsAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
#pragma warning disable CA1031 // GetProcessById throws if PID doesn't exist
        catch
        {
            return false;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Generates a default instance name, <c>&lt;prefix&gt;-&lt;pid&gt;</c>, from the current process ID.
    /// </summary>
    public static string GenerateName(string prefix) =>
        $"{prefix}-{Environment.ProcessId}";

    private static void WriteAtomically(string path, string content)
    {
        var dir = Path.GetDirectoryName(path)!;
        var tmp = Path.Combine(dir, $".{Path.GetFileName(path)}.tmp");
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }
}
