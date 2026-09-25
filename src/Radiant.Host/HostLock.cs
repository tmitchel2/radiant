using System.Globalization;
using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host;

/// <summary>
/// Arbitrates the auto-attach host spawn so two near-simultaneous plain launches don't each bring up a
/// compositing host (which, sharing the fixed <see cref="TabOwnership.PrimaryHostName"/>, would register
/// under the same instance name). The spawn is recorded <i>synchronously</i> under an exclusive file
/// lock by the spawned host's PID: that PID is the durable "a host is coming up" signal which survives
/// the gap before the new host registers its <c>tab</c> capability (window boot is async, so a registry
/// re-check alone can't see it yet). A dead/empty PID in the lock means a prior winner crashed mid-spawn
/// and the host should be (re)spawned.
/// </summary>
public static class HostLock
{
    /// <summary>The arbitration lock, a sibling of the instances directory (<c>&lt;data directory&gt;/host.lock</c>).</summary>
    public static string DefaultLockPath =>
        Path.Combine(Directory.GetParent(InstanceRegistry.RootDir)!.FullName, "host.lock");

    private const int AcquireAttempts = 20;
    private const int AcquireDelayMs = 10;

    /// <summary>
    /// Ensure exactly one host is spawned. <paramref name="hostExists"/> is the fast-path check (a host
    /// already registered its <c>tab</c> capability); <paramref name="isAlive"/> tests a PID;
    /// <paramref name="spawn"/> launches a host and returns its PID. Seams are injected so the logic is
    /// unit-testable without the registry or a live process.
    /// </summary>
    public static void EnsureSingleHost(
        string lockPath,
        Func<bool> hostExists,
        Func<int, bool> isAlive,
        Func<int> spawn)
    {
        // Fast path: a fully-registered host already exists — never touch the lock.
        if (hostExists())
        {
            return;
        }

        using var handle = TryAcquire(lockPath);
        if (handle is null)
        {
            // A racing launcher holds the lock for the duration of its own spawn — it is bringing a host
            // up, so back off. Our own auto-attached renderer is adopted once that host registers.
            return;
        }

        // Re-check under the lock: a prior launcher may already have spawned a host that hasn't finished
        // booting (so hostExists() can't see it yet), recorded by its PID in the lock file.
        var existing = ReadPid(handle);
        if (existing is int pid && isAlive(pid))
        {
            return;
        }

        WritePid(handle, spawn());
    }

    private static FileStream? TryAcquire(string lockPath)
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
