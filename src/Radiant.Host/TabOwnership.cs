using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host;

/// <summary>
/// Tab → host ownership, used for the multi-process window model (Phase 4): a renderer (tab) is owned
/// by at most one host process; tearing a tab out spawns a second host that takes ownership. Ownership
/// is a small <c>owner.txt</c> marker co-located in the renderer's instance directory
/// (<c>&lt;data directory&gt;/instances/&lt;name&gt;/owner.txt</c>) holding the owning host's instance name; it is
/// cleaned up with the instance dir when the renderer exits.
///
/// <para>Rule (<see cref="IsOwnedBy"/>): the <b>primary</b> host (<see cref="PrimaryHostName"/>) adopts
/// any tab whose owner is unset or equal to its own name — so a single default host adopts every
/// renderer, exactly as before. A <b>secondary</b> host (spawned by a tear-off, name != primary) adopts
/// only tabs explicitly assigned to it. This keeps the common single-window case zero-config and makes
/// tear-off an explicit reassignment.</para>
/// </summary>
internal static class TabOwnership
{
    /// <summary>
    /// The default/primary host name (also the value used when no <c>--name</c> is passed): the installed
    /// identity's <see cref="RadiantAppIdentity.PrimaryHostName"/>.
    /// </summary>
    public static string PrimaryHostName => RadiantAppIdentity.Current.PrimaryHostName;

    private static string OwnerPath(string instanceName) =>
        Path.Combine(InstanceRegistry.RootDir, instanceName, "owner.txt");

    /// <summary>Read the owning host name for a tab, or null if unset / unreadable.</summary>
    public static string? ReadOwner(string instanceName)
    {
        var path = OwnerPath(instanceName);
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            var owner = File.ReadAllText(path).Trim();
            return string.IsNullOrEmpty(owner) ? null : owner;
        }
#pragma warning disable CA1031 // Marker may be mid-write / removed; treat as unowned.
        catch
        {
            return null;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Whether the tab's owner marker was (re)assigned within <paramref name="window"/> of now — i.e. the
    /// ownership is "fresh". Used to hold off orphan reclamation right after a tear-off: the just-assigned
    /// owning host may still be booting (not yet in the registry), so it is not really orphaned. Returns
    /// false if the marker is missing / unreadable (then the normal liveness check decides).
    /// </summary>
    public static bool OwnerAssignedWithin(string instanceName, TimeSpan window)
    {
        try
        {
            var path = OwnerPath(instanceName);
            return File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < window;
        }
#pragma warning disable CA1031 // Unreadable timestamp → fall back to the liveness check.
        catch
        {
            return false;
        }
#pragma warning restore CA1031
    }

    /// <summary>Assign a tab to a host (atomic write).</summary>
    public static void WriteOwner(string instanceName, string hostName)
    {
        var path = OwnerPath(instanceName);
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, ".owner.txt.tmp");
        File.WriteAllText(tmp, hostName);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>
    /// Remove a tab's owner marker, returning it to "unowned" so the primary host adopts it again (used
    /// to reclaim a tab whose owning host has died). Best-effort: a missing marker is already the
    /// desired state.
    /// </summary>
    public static void ClearOwner(string instanceName)
    {
        try
        {
            var path = OwnerPath(instanceName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
#pragma warning disable CA1031 // Marker may be mid-write / already removed; the goal state is "no owner".
        catch
        {
            // Best-effort: another rescan reattempts if the delete lost a race.
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Whether a tab is orphaned and the primary host should reclaim it: only the primary reclaims, and
    /// only a tab assigned to a <i>different</i>, no-longer-live host (an abnormally-exited secondary).
    /// Liveness is injected (<paramref name="isHostLive"/>) so the decision is pure and unit-testable;
    /// the live caller resolves it through the instance registry.
    /// </summary>
    public static bool IsOrphaned(string? owner, string hostName, Func<string, bool> isHostLive) =>
        IsPrimary(hostName)
            && !string.IsNullOrEmpty(owner)
            && !string.Equals(owner, hostName, StringComparison.Ordinal)
            && !isHostLive(owner);

    /// <summary>Decide whether <paramref name="hostName"/> should adopt the tab, given its current owner.</summary>
    public static bool IsOwnedBy(string? owner, string hostName) =>
        string.Equals(hostName, PrimaryHostName, StringComparison.Ordinal)
            ? string.IsNullOrEmpty(owner) || string.Equals(owner, hostName, StringComparison.Ordinal)
            : string.Equals(owner, hostName, StringComparison.Ordinal);

    /// <summary>Whether a host name denotes the primary (window that stays alive with zero tabs).</summary>
    public static bool IsPrimary(string hostName) =>
        string.Equals(hostName, PrimaryHostName, StringComparison.Ordinal);
}
