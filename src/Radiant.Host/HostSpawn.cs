using System.Diagnostics;
using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host;

/// <summary>
/// Brings up a compositing host if there is not one, so a plain launch opens a window with a tab
/// in it.
/// </summary>
/// <remarks>
/// <para>
/// Every application built on the host has to do exactly this, and copying it means copying the
/// arbitration too.
/// </para>
/// <para>
/// <b>The spawn is locked, and that is not incidental.</b> A host registers under a fixed primary
/// name, so two plain launches racing would bring up two hosts, the second of which refuses to
/// start and leaves its terminal held. <see cref="HostLock"/> arbitrates.
/// </para>
/// <para>
/// <b>It relaunches the CALLING executable</b>, resolved by <see cref="SelfRelaunch"/> across AOT,
/// single-file, framework-dependent and muxer launches. So the host is the caller's own binary with
/// <c>--type host</c>, which is what makes each application's window carry its own name and icon.
/// </para>
/// </remarks>
public static class HostSpawn
{
    /// <summary>
    /// Launches a host for <paramref name="identity"/>, which it installs for this process, unless one is
    /// already up.
    /// </summary>
    /// <returns>Whether a host was started by this call.</returns>
    public static bool EnsureRunning(RadiantAppIdentity identity)
    {
        RadiantAppIdentity.Use(identity);
        var spawned = false;

        HostLock.EnsureSingleHost(
            HostLock.DefaultLockPath,
            hostExists: () => InstanceRegistry.ListInstances()
                .Any(instance => instance.Capabilities.Contains("tab", StringComparer.Ordinal)),
            isAlive: InstanceRegistry.IsAlive,
            spawn: () =>
            {
                var process = Process.Start(SelfRelaunch.BuildStartInfo("--type", "host"))
                    ?? throw new InvalidOperationException("Failed to launch a compositing host.");

                spawned = true;
                return process.Id;
            });

        return spawned;
    }
}
