using System.Diagnostics;
using System.Text.Json;
using Radiant.Host.AgentControlProtocol;
using Radiant.Host.Ipc;

namespace Radiant.Host;

/// <summary>
/// Owns the host's tab set: the ordered list of attached renderer instances, the active index, and
/// the <c>tab.*</c> agent-control actions (<c>tab.list</c> / <c>tab.activate</c> / <c>tab.spawn</c> /
/// <c>tab.close</c>). The live tab scan, renderer spawn, and renderer close are injectable seams so
/// the controller can be unit-tested without the instance registry, a window, or live processes; the
/// default seams use <see cref="InstanceRegistry"/> + <see cref="Process"/>.
///
/// <para>Tab selection is protocol-version-filtered (see <see cref="TabProtocol"/>): a renderer that
/// publishes frames but advertises a different version is logged once and excluded rather than having
/// its shared buffer opened.</para>
/// </summary>
internal sealed class TabController
{
    private static readonly ActionDefinition[] s_actionDefs =
    [
        new() { Name = "tab.list", Description = "List open tabs (instance names) and the active index", Category = "tab" },
        new() { Name = "tab.activate", Description = "Activate a tab by 'index' (number) or 'name' (string)", Category = "tab" },
        new() { Name = "tab.spawn", Description = "Launch a worktree build with --attach as a new tab (params: buildPath|project, optional name, scenes)", Category = "tab" },
        new() { Name = "tab.close", Description = "Close a tab by 'index' or 'name' (terminates its renderer)", Category = "tab" },
        new() { Name = "tab.detach", Description = "Tear a tab out into its own host window by 'index' or 'name' (spawns a second host owning it)", Category = "tab" },
        new() { Name = "tab.adopt", Description = "Adopt a renderer ('name') owned by another host into this strip (cross-window merge); optional 'cursorX' (global screen-x) picks the drop slot", Category = "tab" },
        new() { Name = "tab.handoff", Description = "Hand a tab ('name') this host owns to another host ('target') and drop it locally (driven by the source host on a live tear-off re-merge); optional 'cursorX' picks the slot", Category = "tab" },
        new() { Name = "window.focus", Description = "Raise and focus this host's window (used by the macOS Dock menu to switch between hosts)", Category = "window" },
        new() { Name = "actions.list", Description = "List available host actions", Category = "meta" },
    ];

    // Grace window after a tear-off during which a just-assigned owner is NOT treated as orphaned, even if
    // it isn't in the registry yet — the new host takes ~1–2 s to boot and call RegisterHost, and reclaiming
    // inside that window would steal the torn-off tab back, leaving its new window empty (an intermittent
    // race). Comfortably larger than host startup; a host that truly dies during startup is reclaimed once
    // the window elapses.
    private static readonly TimeSpan s_ownerStartupGrace = TimeSpan.FromSeconds(8);

    private readonly string _hostName;
    private readonly List<string> _tabs = [];
    private readonly HashSet<string> _warnedIncompatible = new(StringComparer.Ordinal);
    private readonly Func<List<string>> _scan;
    private readonly Func<SpawnRequest, (string Name, int Pid)> _spawn;
    private readonly Func<string, string?, (string Name, int Pid)> _spawnLocal;
    private readonly Func<string, bool> _close;
    private readonly Func<string, bool, (string Host, int Pid)> _detach;
    private readonly Func<string, bool> _adopt;
    private readonly Func<string, string, float?, bool> _handoff;
    private readonly Action? _focusWindow;
    private int _activeIndex;
    private int _spawnCounter;
    private int _detachCounter;

    public TabController(
        string hostName,
        Func<List<string>>? scan = null,
        Func<SpawnRequest, (string Name, int Pid)>? spawn = null,
        Func<string, bool>? close = null,
        Func<string, bool, (string Host, int Pid)>? detach = null,
        Func<string, bool>? adopt = null,
        Func<string, string?, (string Name, int Pid)>? spawnLocal = null,
        Func<string, string, float?, bool>? handoff = null,
        Action? focusWindow = null)
    {
        _hostName = hostName;
        _scan = scan ?? DefaultScan;
        _spawn = spawn ?? DefaultSpawn;
        _close = close ?? DefaultClose;
        _detach = detach ?? DefaultDetach;
        _adopt = adopt ?? DefaultAdopt;
        _spawnLocal = spawnLocal ?? DefaultSpawnLocal;
        _handoff = handoff ?? DefaultHandoff;
        _focusWindow = focusWindow;
    }

    /// <summary>Tabs in strip order.</summary>
    public IReadOnlyList<string> Tabs => _tabs;

    /// <summary>Index of the active tab, or -1 when there are no tabs.</summary>
    public int ActiveIndex => _tabs.Count == 0 ? -1 : Math.Clamp(_activeIndex, 0, _tabs.Count - 1);

    /// <summary>Name of the active tab, or null when there are no tabs.</summary>
    public string? ActiveName => _tabs.Count == 0 ? null : _tabs[ActiveIndex];

    /// <summary>The frame-buffer path a renderer instance publishes to.</summary>
    public static string FramesPath(string instanceName) =>
        Path.Combine(InstanceRegistry.RootDir, instanceName, "frames.bin");

    /// <summary>
    /// Rescan the live tab set, preserving both the existing strip order and the active tab by name.
    /// The in-memory order is authoritative: tabs still alive keep their current position, and renderers
    /// that newly appeared are appended (in scan order) — so a manual reorder (drag-to-reorder) or a
    /// cross-window merge survives the once-a-second rescan rather than snapping back to ordinal-by-name.
    /// Returns true if the tab list changed.
    /// </summary>
    public bool Refresh()
    {
        var live = _scan();
        var liveSet = new HashSet<string>(live, StringComparer.Ordinal);

        var reconciled = new List<string>(_tabs.Count);
        reconciled.AddRange(_tabs.Where(liveSet.Contains));
        var kept = new HashSet<string>(reconciled, StringComparer.Ordinal);
        reconciled.AddRange(live.Where(n => !kept.Contains(n)));

        if (reconciled.SequenceEqual(_tabs))
        {
            return false;
        }

        var previousActive = ActiveName;
        _tabs.Clear();
        _tabs.AddRange(reconciled);
        var newActive = previousActive is not null ? _tabs.IndexOf(previousActive) : -1;
        _activeIndex = newActive >= 0 ? newActive : 0;
        return true;
    }

    /// <summary>
    /// Move the tab at <paramref name="from"/> to the insertion gap <paramref name="to"/>
    /// (0..<see cref="Tabs"/>.Count, as produced by <see cref="TabStripLayout.InsertionIndexAt"/>),
    /// preserving the active tab by name. Returns true if the order changed. Used by the drag-to-reorder
    /// gesture.
    /// </summary>
    public bool Reorder(int from, int to)
    {
        if (from < 0 || from >= _tabs.Count)
        {
            return false;
        }
        to = Math.Clamp(to, 0, _tabs.Count);
        // Removing 'from' shifts everything after it left by one, so a gap past 'from' lands one index
        // lower once the tab is pulled out.
        if (to > from)
        {
            to--;
        }
        if (to == from)
        {
            return false;
        }

        var activeName = ActiveName;
        var moved = _tabs[from];
        _tabs.RemoveAt(from);
        _tabs.Insert(to, moved);
        if (activeName is not null)
        {
            _activeIndex = _tabs.IndexOf(activeName);
        }
        return true;
    }

    /// <summary>
    /// Spawn a new renderer tab attached to <i>this</i> host (the strip's + button): relaunch this build
    /// with <c>--attach</c>, pre-assigning ownership to this host so it adopts the newcomer on the next
    /// rescan (a secondary host adopts its own new tab, not just the primary). Returns the new tab's
    /// name + pid. When <paramref name="scenePath"/> is given (File ▸ New / Open / Open Recent), the
    /// newcomer boots straight into that scene file or folder.
    /// </summary>
    public (string Name, int Pid) SpawnLocalTab(string? scenePath = null) => _spawnLocal(NextSpawnName(), scenePath);

    /// <summary>Activate the tab at <paramref name="index"/>. Returns true if the active tab changed.</summary>
    public bool Activate(int index)
    {
        if (index < 0 || index >= _tabs.Count || index == ActiveIndex)
        {
            return false;
        }
        _activeIndex = index;
        return true;
    }

    /// <summary>Dispatch a <c>tab.*</c> (or <c>actions.list</c>) command and return its response.</summary>
    public AgentResponse Handle(AgentCommand cmd)
    {
        try
        {
            return cmd.Action switch
            {
                "tab.list" => HandleList(cmd),
                "tab.activate" => HandleActivate(cmd),
                "tab.spawn" => HandleSpawn(cmd),
                "tab.close" => HandleClose(cmd),
                "tab.detach" => HandleDetach(cmd),
                "tab.adopt" => HandleAdopt(cmd),
                "tab.handoff" => HandleHandoff(cmd),
                "window.focus" => HandleFocus(cmd),
                "actions.list" => AgentResponse.Ok(cmd.Id, JsonSerializer.SerializeToElement(s_actionDefs, AgentJsonContext.Default.ActionDefinitionArray), 0),
                _ => AgentResponse.Err(cmd.Id, "not_found", $"Unknown action: {cmd.Action}"),
            };
        }
        catch (ArgumentException ex)
        {
            return AgentResponse.Err(cmd.Id, "invalid_params", ex.Message);
        }
#pragma warning disable CA1031 // Surface any handler failure as a clean error response.
        catch (Exception ex)
        {
            return AgentResponse.Err(cmd.Id, "internal", ex.Message);
        }
#pragma warning restore CA1031
    }

    private AgentResponse HandleList(AgentCommand cmd)
    {
        Refresh();
        return AgentResponse.Ok(cmd.Id, SerializeList(), 0);
    }

    private AgentResponse HandleActivate(AgentCommand cmd)
    {
        Refresh();
        var idx = HostActions.ResolveTabIndex(_tabs, cmd.Params);
        Activate(idx);
        return AgentResponse.Ok(cmd.Id, SerializeList(), 0);
    }

    private AgentResponse HandleSpawn(AgentCommand cmd)
    {
        var request = HostActions.ParseSpawnRequest(cmd.Params, NextSpawnName);
        var (name, pid) = _spawn(request);
        var result = new TabSpawnResult { Name = name, Pid = pid };
        return AgentResponse.Ok(cmd.Id, JsonSerializer.SerializeToElement(result, HostJsonContext.Default.TabSpawnResult), 0);
    }

    private AgentResponse HandleClose(AgentCommand cmd)
    {
        Refresh();
        var idx = HostActions.ResolveTabIndex(_tabs, cmd.Params);
        CloseTab(idx);
        return AgentResponse.Ok(cmd.Id, SerializeList(), 0);
    }

    /// <summary>
    /// Close the tab at <paramref name="index"/>: terminate its renderer and drop it from the strip,
    /// keeping the active index in range. Returns true if a tab was closed. Used by both the
    /// <c>tab.close</c> action and the strip's close (×) button.
    /// </summary>
    public bool CloseTab(int index)
    {
        if (index < 0 || index >= _tabs.Count)
        {
            return false;
        }
        var name = _tabs[index];
        _close(name);
        _tabs.RemoveAt(index);
        if (_activeIndex >= _tabs.Count)
        {
            _activeIndex = Math.Max(0, _tabs.Count - 1);
        }
        return true;
    }

    private AgentResponse HandleDetach(AgentCommand cmd)
    {
        Refresh();
        var idx = HostActions.ResolveTabIndex(_tabs, cmd.Params);
        var (host, pid) = DetachTab(idx);
        var result = new TabSpawnResult { Name = host, Pid = pid };
        return AgentResponse.Ok(cmd.Id, JsonSerializer.SerializeToElement(result, HostJsonContext.Default.TabSpawnResult), 0);
    }

    private AgentResponse HandleAdopt(AgentCommand cmd)
    {
        Refresh();
        var (name, cursorX) = HostActions.ParseAdoptRequest(cmd.Params);
        AdoptTab(name, cursorX);
        return AgentResponse.Ok(cmd.Id, SerializeList(), 0);
    }

    private AgentResponse HandleFocus(AgentCommand cmd)
    {
        if (_focusWindow is null)
        {
            return AgentResponse.Err(cmd.Id, "unsupported", "Window focus is not available on this host.");
        }
        _focusWindow();
        return AgentResponse.Ok(cmd.Id, null, 0);
    }

    private AgentResponse HandleHandoff(AgentCommand cmd)
    {
        Refresh();
        var (name, target, cursorX) = HostActions.ParseHandoffRequest(cmd.Params);
        HandoffTab(name, target, cursorX);
        return AgentResponse.Ok(cmd.Id, SerializeList(), 0);
    }

    /// <summary>
    /// Hand <paramref name="name"/> (a tab this host owns) to host <paramref name="target"/> and drop it
    /// locally — the receiving side runs the normal <c>tab.adopt</c> merge. Used when the source host (which
    /// owns the held mouse button) tells this torn-off follower window where to re-merge on release. After
    /// the local drop a now-empty follower host self-closes. Returns true if the handoff succeeded.
    /// </summary>
    public bool HandoffTab(string name, string target, float? cursorX)
    {
        var idx = _tabs.IndexOf(name);
        if (idx < 0)
        {
            return false;
        }
        if (!_handoff(name, target, cursorX))
        {
            return false; // target unreachable / refused — keep the tab; this window stays standalone
        }
        Forget(idx);
        return true;
    }

    /// <summary>
    /// Tear the tab at <paramref name="index"/> out into its own host window: assign it to a fresh host
    /// name, spawn that host, and drop the tab locally (a rescan reconciles once ownership changes).
    /// Returns the new host's name + pid. Used by both the <c>tab.detach</c> action and the tear-off
    /// gesture.
    /// </summary>
    public (string Host, int Pid) DetachTab(int index) => DetachTab(index, follow: false);

    /// <summary>
    /// Tear the tab at <paramref name="index"/> out into its own host window. When <paramref name="follow"/>
    /// is true the new host is spawned in follow mode (hidden until it positions itself under the cursor) for
    /// a live, mid-drag tear-off; otherwise it opens as a normal window (the release-time <c>tab.detach</c>).
    /// </summary>
    public (string Host, int Pid) DetachTab(int index, bool follow)
    {
        // Invariant: a tear-off must never empty its source host (a host is never left with zero tabs).
        // A host's only tab is already in its own window, so there is nothing to tear off — the sole-tab
        // drag gesture moves the whole window instead (see LiveHost window-drag mode).
        if (_tabs.Count <= 1)
        {
            throw new InvalidOperationException(
                "Cannot tear off the only tab — a host must never be left with zero tabs.");
        }
        var name = _tabs[index];
        var result = _detach(name, follow);
        _tabs.RemoveAt(index);
        if (_activeIndex >= _tabs.Count)
        {
            _activeIndex = Math.Max(0, _tabs.Count - 1);
        }
        return result;
    }

    /// <summary>
    /// Adopt <paramref name="name"/> (a renderer currently owned by another host) into this strip: take
    /// ownership, insert it at the slot under <paramref name="cursorX"/> (global screen-x; appended when
    /// the drop position or this window's rect is unknown), and activate it. The cross-process inverse of
    /// <see cref="DetachTab(int)"/> — the receiving side of a drag-merge, surfaced as <c>tab.adopt</c>.
    /// </summary>
    public void AdoptTab(string name, float? cursorX)
    {
        _adopt(name);
        if (!_tabs.Contains(name))
        {
            _tabs.Insert(ResolveAdoptSlot(cursorX), name);
        }
        _activeIndex = _tabs.IndexOf(name);
    }

    /// <summary>
    /// Drop a tab from this strip locally without terminating its renderer — the source side of a merge,
    /// once the target host has taken ownership (a rescan would drop it anyway; this is immediate).
    /// </summary>
    public bool Forget(int index)
    {
        if (index < 0 || index >= _tabs.Count)
        {
            return false;
        }
        _tabs.RemoveAt(index);
        if (_activeIndex >= _tabs.Count)
        {
            _activeIndex = Math.Max(0, _tabs.Count - 1);
        }
        return true;
    }

    private int ResolveAdoptSlot(float? cursorX)
    {
        if (cursorX is not { } gx)
        {
            return _tabs.Count;
        }
        var bounds = HostWindowBounds.Read(_hostName);
        if (bounds is null || bounds.Width <= 0)
        {
            return _tabs.Count;
        }
        return new TabStripLayout(bounds.Width, _tabs.Count).InsertionIndexAt(gx - bounds.X);
    }

    private JsonElement SerializeList()
    {
        var result = new TabListResult
        {
            ActiveIndex = ActiveIndex,
            Active = ActiveName,
            Tabs = [.. _tabs.Select((n, i) => new TabInfo { Index = i, Name = n })],
        };
        return JsonSerializer.SerializeToElement(result, HostJsonContext.Default.TabListResult);
    }

    private string NextSpawnName() => $"tab-{Environment.ProcessId}-{++_spawnCounter}";

    private string NextHostName() => $"{TabOwnership.PrimaryHostName}-{Environment.ProcessId}-{++_detachCounter}";

    private (string Host, int Pid) DefaultDetach(string tabName, bool follow)
    {
        var newHost = NextHostName();
        // Assign the tab to the new host BEFORE spawning, so the new host adopts it and this host drops
        // it on the next rescan (ownership check excludes a tab whose owner != this host).
        TabOwnership.WriteOwner(tabName, newHost);

        // The host runs inside the application's single binary (this entry assembly), selected by `--type
        // host`; relaunch it as a new compositing window that owns the torn-off tab. SelfRelaunch resolves
        // the right executable for AOT/single-file/muxer launches (a hard-coded `dotnet` broke published
        // tear-off with "dotnet---type does not exist"). For a live tear-off, `--follow` starts the host
        // hidden + focus-off so it can position itself under the cursor before showing (no spawn-origin
        // flash, no focus-steal that would end the drag).
        var psi = follow
            ? SelfRelaunch.BuildStartInfo("--type", "host", "--name", newHost, "--follow")
            : SelfRelaunch.BuildStartInfo("--type", "host", "--name", newHost);
        var proc = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to spawn host window for tab '{tabName}'.");
        return (newHost, proc.Id);
    }

    private static bool DefaultHandoff(string tabName, string target, float? cursorX)
    {
        // The receiving side runs the normal cross-window merge: take ownership + insert at the drop slot.
        var p = new System.Text.Json.Nodes.JsonObject { ["name"] = tabName };
        if (cursorX is { } cx)
        {
            p["cursorX"] = cx;
        }
        try
        {
            var resp = new CommandClient(target).Send("tab.adopt", p.ToJsonString(), timeoutMs: 3000);
            return string.Equals(resp.Status, "ok", StringComparison.Ordinal);
        }
#pragma warning disable CA1031 // A failed handoff must not crash the host; the follower stays standalone.
        catch
        {
            return false;
        }
#pragma warning restore CA1031
    }

    private (string Name, int Pid) DefaultSpawnLocal(string name, string? scenePath)
    {
        // Assign ownership to this host BEFORE the renderer attaches, so this host (primary or secondary)
        // adopts it on the next rescan rather than the primary grabbing an unowned newcomer.
        TabOwnership.WriteOwner(name, _hostName);
        // Launch a renderer the way the application says to: by default this same binary relaunched as a
        // headless `--attach` tab (RadiantAppIdentity.RelaunchAsTab), with the path to open forwarded so the
        // new tab boots straight into it.
        var psi = RadiantAppIdentity.Current.TabLaunch(name, scenePath);
        var proc = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to spawn local tab '{name}'.");
        return (name, proc.Id);
    }

    private bool DefaultAdopt(string tabName)
    {
        // Take ownership by reassigning the renderer's owner marker to this host. The source host drops
        // it on its next rescan (owner != self); this host already inserted it in AdoptTab.
        TabOwnership.WriteOwner(tabName, _hostName);
        return true;
    }

    private List<string> DefaultScan() =>
        [.. HostActions.SelectTabs(InstanceRegistry.ListInstances(), n => File.Exists(FramesPath(n)), TabProtocol.Version, WarnIncompatible)
            .Where(IsAdoptable)];

    /// <summary>
    /// Whether this host should adopt <paramref name="tabName"/>. The primary additionally reclaims a tab
    /// whose owning host has died (clearing the stale marker so the standard ownership check then adopts
    /// it) — otherwise a renderer torn off to a since-crashed host would keep running, owned by nobody.
    /// </summary>
    private bool IsAdoptable(string tabName)
    {
        var owner = TabOwnership.ReadOwner(tabName);
        // Reclaim a tab whose owning host has died — but NOT during the post-tear-off grace window, when the
        // owner may simply still be booting (not yet in the registry). Reclaiming then would steal a
        // just-torn-off tab before its new window starts, leaving that window empty (the intermittent race).
        if (TabOwnership.IsOrphaned(owner, _hostName, HostLive)
            && !TabOwnership.OwnerAssignedWithin(tabName, s_ownerStartupGrace))
        {
            TabOwnership.ClearOwner(tabName);
            owner = null;
        }
        return TabOwnership.IsOwnedBy(owner, _hostName);
    }

    // GetInstance reads the owner's registered PID and reuses InstanceRegistry.IsAlive (and auto-cleans a
    // dead entry), returning null when the host is gone — the owner's own PID lives in its instance.json,
    // not in its name, so the registry is the source of truth for liveness.
    private static bool HostLive(string hostName) => InstanceRegistry.GetInstance(hostName) is not null;

    private void WarnIncompatible(InstanceInfo info)
    {
        var key = $"{info.Name}:{info.ProtocolVersion}";
        if (_warnedIncompatible.Add(key))
        {
            Console.Error.WriteLine(
                $"Radiant.Host: skipping tab '{info.Name}' — protocol version {info.ProtocolVersion} " +
                $"is incompatible with host version {TabProtocol.Version}. Rebuild that worktree to attach it.");
        }
    }

    private static (string Name, int Pid) DefaultSpawn(SpawnRequest request)
    {
        var projectArg = HostActions.ResolveProjectArg(request.BuildPath, RadiantAppIdentity.Current.WorktreeProject, File.Exists);
        var args = HostActions.BuildSpawnArguments(request, projectArg);
        var psi = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }
        var proc = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to launch renderer for '{request.Name}'.");
        return (request.Name, proc.Id);
    }

    private static bool DefaultClose(string name)
    {
        var info = InstanceRegistry.GetInstance(name);
        if (info is not null)
        {
            try
            {
                using var proc = Process.GetProcessById(info.Pid);
                proc.Kill(entireProcessTree: true);
            }
#pragma warning disable CA1031 // Process may already be gone; cleanup is best-effort.
            catch
            {
                // Already exited / not ours — fall through to deregister.
            }
#pragma warning restore CA1031
        }
        InstanceRegistry.Deregister(name);
        return true;
    }
}
