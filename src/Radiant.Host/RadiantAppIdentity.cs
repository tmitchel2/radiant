using System.Diagnostics;
using Radiant.Host.AgentControlProtocol;
using Radiant.Host.Ipc.Recents;

namespace Radiant.Host;

/// <summary>
/// Who the application is: the name on its windows, where its processes meet on disk, what its instances
/// are called, its Dock icon, what File ▸ New and Open… do, and how a new renderer tab is launched.
/// Everything the host shell would otherwise have to hard-code about one particular application.
/// </summary>
/// <remarks>
/// <para>
/// <b>An identity is process-wide, and every process of an application must install the same one.</b>
/// The host, its renderer tabs, the drag overlay and any control CLI are separate processes that find each
/// other through <see cref="DataDirectory"/>; a renderer that installed a different identity would publish
/// its frames where the host never looks. The host entry points (<see cref="HostEntry.Run"/>,
/// <see cref="DragOverlayEntry.Run"/>, <see cref="HostSpawn.EnsureRunning"/>) take the identity and install
/// it; a renderer or a CLI calls <see cref="Use"/> itself before touching the registry.
/// </para>
/// <para>
/// Until an application installs its own, <see cref="Current"/> is <see cref="Default"/>: a generic
/// Radiant application under <c>~/.radiant</c>.
/// </para>
/// </remarks>
public sealed record RadiantAppIdentity
{
    /// <summary>The application's name: the host window's title and the prefix of its log lines.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The directory every process of the application shares: the instance registry
    /// (<c>instances/</c>), the host and Dock-owner locks, the drag session, and <c>recent.json</c>.
    /// </summary>
    public required string DataDirectory { get; init; }

    /// <summary>
    /// The stem of the application's instance names: the primary host is <c>&lt;prefix&gt;-host</c>, a
    /// generated instance <c>&lt;prefix&gt;-&lt;pid&gt;</c>, the drag overlay <c>&lt;prefix&gt;-drag-overlay</c>.
    /// </summary>
    public required string InstancePrefix { get; init; }

    /// <summary>
    /// The stem of the host's diagnostic switches: <c>&lt;prefix&gt;_DRAG_DEBUG</c>,
    /// <c>&lt;prefix&gt;_DRAG_OVERLAY</c> and <c>&lt;prefix&gt;_DOCK_CONSOLIDATE</c>.
    /// </summary>
    public required string EnvironmentPrefix { get; init; }

    /// <summary>The PNG bytes of the Dock icon, or null for the platform's generic executable icon.</summary>
    public required Func<byte[]?> DockIcon { get; init; }

    /// <summary>
    /// The file extensions (without the dot) File ▸ Open… lets the user pick. Folders are always
    /// selectable. Empty allows any file.
    /// </summary>
    public required IReadOnlyList<string> OpenFileExtensions { get; init; }

    /// <summary>
    /// Creates a new document for File ▸ New and returns the path to open in a new tab, or null for a tab
    /// with nothing open in it. Null hides File ▸ New.
    /// </summary>
    public required Func<string?>? NewDocument { get; init; }

    /// <summary>
    /// The process that renders one tab: given the instance name the renderer must register under, and the
    /// path to open (or null), the start info for a renderer of this application.
    /// </summary>
    public required Func<string, string?, ProcessStartInfo> TabLaunch { get; init; }

    /// <summary>
    /// For <c>tab.spawn</c> given a worktree root, the application's project relative to that root (for
    /// example <c>src/MyApp/MyApp.csproj</c>), or null when <c>tab.spawn</c> must be given a project.
    /// </summary>
    public required string? WorktreeProject { get; init; }

    /// <summary>The instance registry's root, <c>&lt;DataDirectory&gt;/instances</c>.</summary>
    public string InstancesDirectory => Path.Combine(DataDirectory, "instances");

    /// <summary>The name the primary compositing host registers under.</summary>
    public string PrimaryHostName => $"{InstancePrefix}-host";

    /// <summary>The name the floating drag overlay registers under.</summary>
    public string OverlayInstanceName => $"{InstancePrefix}-drag-overlay";

    /// <summary>A default instance name for this process.</summary>
    public string GenerateInstanceName() => InstanceRegistry.GenerateName(InstancePrefix);

    /// <summary>Whether the diagnostic switch <c>&lt;EnvironmentPrefix&gt;_<paramref name="suffix"/></c> is <paramref name="value"/>.</summary>
    public bool EnvironmentIs(string suffix, string value) =>
        string.Equals(Environment.GetEnvironmentVariable($"{EnvironmentPrefix}_{suffix}"), value, StringComparison.Ordinal);

    /// <summary>
    /// A generic Radiant application: <c>~/.radiant</c>, <c>radiant-*</c> instances, Radiant's own Dock
    /// icon, any file for Open…, no File ▸ New, and tabs rendered by relaunching this executable with
    /// <c>--attach --name &lt;name&gt; [--scene &lt;path&gt;]</c>.
    /// </summary>
    public static RadiantAppIdentity Default { get; } = new()
    {
        Name = "Radiant",
        DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".radiant"),
        InstancePrefix = "radiant",
        EnvironmentPrefix = "RADIANT",
        DockIcon = MacDockIcon.LoadRadiantIcon,
        OpenFileExtensions = [],
        NewDocument = null,
        TabLaunch = RelaunchAsTab,
        WorktreeProject = null,
    };

    /// <summary>The identity this process has installed; <see cref="Default"/> until one is.</summary>
    public static RadiantAppIdentity Current { get; private set; } = Default;

    /// <summary>
    /// Installs <paramref name="identity"/> for this process: points the instance registry and the recent
    /// files store at its <see cref="DataDirectory"/>. Call before anything touches the registry.
    /// </summary>
    public static void Use(RadiantAppIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Current = identity;
        InstanceRegistry.RootDir = identity.InstancesDirectory;
        RecentFilesStore.StoreDir = identity.DataDirectory;
    }

    /// <summary>
    /// The conventional renderer launch: this executable again, with <c>--attach --name &lt;name&gt;</c>
    /// and, when there is something to open, <c>--scene &lt;path&gt;</c>.
    /// </summary>
    public static ProcessStartInfo RelaunchAsTab(string instanceName, string? path) =>
        path is null
            ? SelfRelaunch.BuildStartInfo("--attach", "--name", instanceName)
            : SelfRelaunch.BuildStartInfo("--attach", "--name", instanceName, "--scene", path);
}
