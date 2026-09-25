using System.Globalization;
using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host;

/// <summary>
/// Arbitrates which single host process owns the macOS Dock tile, so two or more concurrent hosts
/// (the primary plus any tear-off secondaries) appear as <b>one</b> Dock icon rather than one tile each.
/// The winner keeps the <c>Regular</c> activation policy, brands the icon, and installs the per-host Dock
/// menu; every other host drops to an <c>Accessory</c> app (no Dock tile) via
/// <see cref="MacDockIcon.TrySetAccessoryPolicy"/>.
///
/// <para>Ownership is recorded as the owner's PID in <c>&lt;data directory&gt;/dock-owner.lock</c> (a sibling of the
/// instances directory, mirroring <see cref="HostLock"/>'s <c>host.lock</c>). The PID — not a held file
/// handle — is the durable signal so a survivor can re-elect itself when the recorded owner dies: a dead
/// PID means "no owner", and the caller claims it. Liveness is injected so the logic is unit-testable
/// without a live process; the live caller resolves it through <see cref="InstanceRegistry.IsAlive"/>.</para>
/// </summary>
public static class DockOwnerLock
{
    /// <summary>The Dock-owner record, a sibling of the instances directory (<c>&lt;data directory&gt;/dock-owner.lock</c>).</summary>
    public static string DefaultLockPath =>
        Path.Combine(Directory.GetParent(InstanceRegistry.RootDir)!.FullName, "dock-owner.lock");

    private const int AcquireAttempts = 20;
    private const int AcquireDelayMs = 10;

    /// <summary>
    /// Try to become the Dock owner. Returns true if this process now owns the Dock tile — either because
    /// it already did, or because no live owner was recorded and it has just claimed ownership. Returns
    /// false while another live host owns the tile, or if the lock could not be taken this call (a racing
    /// host holds it briefly — retry on the next tick).
    /// </summary>
    public static bool TryAcquire() => TryAcquire(DefaultLockPath, InstanceRegistry.IsAlive);

    /// <summary>Testable core: <paramref name="isAlive"/> tests whether a recorded owner PID is still live.</summary>
    public static bool TryAcquire(string lockPath, Func<int, bool> isAlive)
    {
        using var handle = Open(lockPath);
        if (handle is null)
        {
            // A racing host holds the lock for the duration of its own claim — back off and retry later.
            return false;
        }

        var me = Environment.ProcessId;
        var existing = ReadPid(handle);
        if (existing is int pid)
        {
            if (pid == me)
            {
                return true; // we already own it
            }
            if (isAlive(pid))
            {
                return false; // a live host owns the Dock tile
            }
        }

        // No live owner (unset, or the previous owner died) — claim it.
        WritePid(handle, me);
        return true;
    }

    private static FileStream? Open(string lockPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        for (var attempt = 0; attempt < AcquireAttempts; attempt++)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                Thread.Sleep(AcquireDelayMs);
            }
        }
        return null;
    }

    private static int? ReadPid(FileStream handle)
    {
        handle.Position = 0;
        using var reader = new StreamReader(handle, leaveOpen: true);
        var text = reader.ReadToEnd().Trim();
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) ? pid : null;
    }

    private static void WritePid(FileStream handle, int pid)
    {
        handle.Position = 0;
        handle.SetLength(0);
        using var writer = new StreamWriter(handle, leaveOpen: true);
        writer.Write(pid.ToString(CultureInfo.InvariantCulture));
        writer.Flush();
    }
}
