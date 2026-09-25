using System.Text.Json;
using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host;

/// <summary>
/// Reads/writes the cross-process drag-session state at <c>&lt;data directory&gt;/drag.json</c> (a sibling of the
/// instances dir, like <c>host.lock</c>). The source host writes it each frame while a tab drag is in
/// flight; the floating <see cref="DragOverlay"/> process polls it to position + paint the ghost. This
/// is the control plane for the drag overlay — the per-frame pixels still come over the dragged
/// renderer's <see cref="Radiant.Host.Ipc.Frames.SharedFrameBuffer"/>.
/// </summary>
internal static class DragSession
{
    /// <summary>Path of the drag-session file (<c>&lt;data directory&gt;/drag.json</c>).</summary>
    public static string SessionPath =>
        Path.Combine(Directory.GetParent(InstanceRegistry.RootDir)!.FullName, "drag.json");

    /// <summary>Publish (atomically) the current drag state.</summary>
    public static void Write(DragSessionState state)
    {
        var path = SessionPath;
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(state, HostJsonContext.Default.DragSessionState);
        var tmp = Path.Combine(dir, ".drag.json.tmp");
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>Mark the drag as ended (overlay hides). Writing an inactive state is preferred over
    /// deleting so a polling overlay reads a definitive "inactive" rather than a transient missing file.</summary>
    public static void Clear() => Write(new DragSessionState { Active = false });

    /// <summary>Read the current drag state, or null if unset / unreadable (treated as inactive).</summary>
    public static DragSessionState? Read()
    {
        var path = SessionPath;
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            return JsonSerializer.Deserialize(File.ReadAllText(path), HostJsonContext.Default.DragSessionState);
        }
#pragma warning disable CA1031 // Mid-write / malformed → treat as inactive.
        catch
        {
            return null;
        }
#pragma warning restore CA1031
    }
}
