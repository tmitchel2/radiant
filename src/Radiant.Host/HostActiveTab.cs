using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host;

/// <summary>
/// Publishes a host's currently-selected tab name to a small <c>active-tab.txt</c> marker co-located in
/// the host's instance directory (<c>&lt;data directory&gt;/instances/&lt;host&gt;/active-tab.txt</c>), mirroring the
/// <see cref="TabOwnership"/> <c>owner.txt</c> side-channel. The Dock-owner host reads every live host's
/// marker when macOS asks it to build the right-click Dock menu, so each menu entry can be labelled with
/// that host's active tab without a synchronous round-trip to the host. Cleaned up with the instance dir
/// when the host exits.
/// </summary>
internal static class HostActiveTab
{
    private static string FilePath(string hostName) =>
        Path.Combine(InstanceRegistry.RootDir, hostName, "active-tab.txt");

    /// <summary>Publish the host's active tab name (atomic write); an empty marker means "no active tab".</summary>
    public static void Write(string hostName, string? activeTabName)
    {
        try
        {
            var path = FilePath(hostName);
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);
            var tmp = Path.Combine(dir, ".active-tab.txt.tmp");
            File.WriteAllText(tmp, activeTabName ?? string.Empty);
            File.Move(tmp, path, overwrite: true);
        }
#pragma warning disable CA1031 // Publishing the label is cosmetic (Dock-menu only); never let it crash the host.
        catch
        {
            // Best-effort: the menu falls back to the host name when the marker is missing.
        }
#pragma warning restore CA1031
    }

    /// <summary>Read a host's active tab name, or null if unset / unreadable.</summary>
    public static string? Read(string hostName)
    {
        try
        {
            var path = FilePath(hostName);
            if (!File.Exists(path))
            {
                return null;
            }
            var name = File.ReadAllText(path).Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }
#pragma warning disable CA1031 // Marker may be mid-write / removed; treat as "no active tab".
        catch
        {
            return null;
        }
#pragma warning restore CA1031
    }
}
