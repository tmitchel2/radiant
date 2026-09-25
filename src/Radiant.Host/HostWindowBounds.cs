using System.Text.Json;
using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host;

/// <summary>
/// Reads/writes a host's window rectangle to a <c>window.json</c> side-channel in its instance dir
/// (<c>&lt;data directory&gt;/instances/&lt;host&gt;/window.json</c>), mirroring the <c>owner.txt</c> pattern. A
/// dragging host reads other hosts' bounds to find the drop target under the global cursor; each host
/// keeps its own bounds current as the window moves/resizes. Kept off <see cref="InstanceInfo"/> so a
/// window move doesn't churn <c>instance.json</c> (and its FileSystemWatcher consumers).
/// </summary>
internal static class HostWindowBounds
{
    private static string BoundsPath(string hostName) =>
        Path.Combine(InstanceRegistry.RootDir, hostName, "window.json");

    /// <summary>Publish (atomically) this host's current window rectangle.</summary>
    public static void Write(string hostName, WindowBounds bounds)
    {
        var path = BoundsPath(hostName);
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(bounds, HostJsonContext.Default.WindowBounds);
        var tmp = Path.Combine(dir, ".window.json.tmp");
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>Read a host's published window rectangle, or null if unset / unreadable.</summary>
    public static WindowBounds? Read(string hostName)
    {
        var path = BoundsPath(hostName);
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            return JsonSerializer.Deserialize(File.ReadAllText(path), HostJsonContext.Default.WindowBounds);
        }
#pragma warning disable CA1031 // Bounds may be mid-write / malformed; treat as "no known rect".
        catch
        {
            return null;
        }
#pragma warning restore CA1031
    }
}
