using System.Text.Json;
using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host;

/// <summary>
/// A request to launch a worktree build as a new tab renderer (parsed from <c>tab.spawn</c> params).
/// </summary>
/// <param name="BuildPath">Path to an application project/worktree to run with <c>--attach</c>.</param>
/// <param name="Name">Instance name the renderer registers under (becomes the tab label).</param>
/// <param name="Scenes">Optional scenes directory passed through as <c>--scenes</c>.</param>
internal sealed record SpawnRequest(string BuildPath, string Name, string? Scenes);

/// <summary>
/// Pure helpers for the host's <c>tab.*</c> control actions: parameter parsing, tab-target
/// resolution, candidate-tab selection (protocol-version filtering), and process-launch argument
/// construction. Kept side-effect-free (file/process effects are injected by the caller) so they can
/// be unit-tested without a window, a GPU, or live processes.
/// </summary>
internal static class HostActions
{
    /// <summary>
    /// Resolve a <c>tab.activate</c>/<c>tab.close</c> target to a tab index. Accepts either an
    /// <c>index</c> (zero-based) or a <c>name</c>. Throws <see cref="ArgumentException"/> with a clear
    /// message when neither is supplied, the index is out of range, or the name is unknown.
    /// </summary>
    public static int ResolveTabIndex(IReadOnlyList<string> tabs, JsonElement? p)
    {
        if (p is { } e && e.ValueKind == JsonValueKind.Object)
        {
            if (e.TryGetProperty("index", out var idxEl) && idxEl.ValueKind == JsonValueKind.Number)
            {
                var idx = idxEl.GetInt32();
                if (idx < 0 || idx >= tabs.Count)
                {
                    throw new ArgumentException($"Tab index {idx} out of range (0..{tabs.Count - 1}).");
                }
                return idx;
            }

            if (e.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
            {
                var name = nameEl.GetString()!;
                var found = -1;
                for (var i = 0; i < tabs.Count; i++)
                {
                    if (string.Equals(tabs[i], name, StringComparison.Ordinal))
                    {
                        found = i;
                        break;
                    }
                }
                if (found < 0)
                {
                    throw new ArgumentException($"No tab named '{name}'.");
                }
                return found;
            }
        }

        throw new ArgumentException("Expected an 'index' (number) or 'name' (string) parameter.");
    }

    /// <summary>
    /// Parse a <c>tab.spawn</c> request. <c>buildPath</c> (alias <c>project</c>) is required; <c>name</c>
    /// defaults to <paramref name="defaultName"/>; <c>scenes</c> (alias <c>scene</c>) is optional.
    /// </summary>
    public static SpawnRequest ParseSpawnRequest(JsonElement? p, Func<string> defaultName)
    {
        if (p is not { ValueKind: JsonValueKind.Object } e)
        {
            throw new ArgumentException("tab.spawn requires a params object with 'buildPath'.");
        }

        var buildPath = GetString(e, "buildPath") ?? GetString(e, "project");
        if (string.IsNullOrWhiteSpace(buildPath))
        {
            throw new ArgumentException("tab.spawn requires a non-empty 'buildPath' (or 'project').");
        }

        var name = GetString(e, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            name = defaultName();
        }

        var scenes = GetString(e, "scenes") ?? GetString(e, "scene");
        return new SpawnRequest(buildPath, name!, scenes);
    }

    /// <summary>
    /// Parse a <c>tab.adopt</c> request: a required <c>name</c> (the renderer to merge in) and an
    /// optional <c>cursorX</c> (global screen-x of the drop, used to pick the insertion slot).
    /// </summary>
    public static (string Name, float? CursorX) ParseAdoptRequest(JsonElement? p)
    {
        if (p is not { ValueKind: JsonValueKind.Object } e)
        {
            throw new ArgumentException("tab.adopt requires a params object with 'name'.");
        }

        var name = GetString(e, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("tab.adopt requires a non-empty 'name'.");
        }

        float? cursorX = e.TryGetProperty("cursorX", out var cx) && cx.ValueKind == JsonValueKind.Number
            ? cx.GetSingle()
            : null;
        return (name!, cursorX);
    }

    /// <summary>
    /// Parse a <c>tab.handoff</c> request: a required <c>name</c> (the tab to hand off) and <c>target</c>
    /// (the receiving host), plus an optional <c>cursorX</c> (global screen-x for the drop slot).
    /// </summary>
    public static (string Name, string Target, float? CursorX) ParseHandoffRequest(JsonElement? p)
    {
        if (p is not { ValueKind: JsonValueKind.Object } e)
        {
            throw new ArgumentException("tab.handoff requires a params object with 'name' and 'target'.");
        }

        var name = GetString(e, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("tab.handoff requires a non-empty 'name'.");
        }

        var target = GetString(e, "target");
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("tab.handoff requires a non-empty 'target'.");
        }

        float? cursorX = e.TryGetProperty("cursorX", out var cx) && cx.ValueKind == JsonValueKind.Number
            ? cx.GetSingle()
            : null;
        return (name!, target!, cursorX);
    }

    /// <summary>
    /// Resolve the value to pass to <c>dotnet run --project</c> for a build path. Accepts an explicit
    /// <c>.csproj</c>, a worktree root (resolves to the directory of <paramref name="worktreeProject"/> under
    /// it, when the application names one), or a project directory. Falls through to the raw path so
    /// <c>dotnet</c> surfaces a clear error if nothing matches.
    /// </summary>
    public static string ResolveProjectArg(string buildPath, string? worktreeProject, Func<string, bool> fileExists)
    {
        if (buildPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || worktreeProject is null)
        {
            return buildPath;
        }

        var worktreeProj = Path.Combine(buildPath, worktreeProject);
        return fileExists(worktreeProj) ? Path.GetDirectoryName(worktreeProj)! : buildPath;
    }

    /// <summary>
    /// Build the <c>dotnet run</c> argument list to launch a renderer for <paramref name="request"/>
    /// in <c>--attach</c> mode under its requested name.
    /// </summary>
    public static List<string> BuildSpawnArguments(SpawnRequest request, string projectArg)
    {
        var args = new List<string> { "run", "--project", projectArg, "--", "--attach", "--name", request.Name };
        if (!string.IsNullOrWhiteSpace(request.Scenes))
        {
            args.Add("--scenes");
            args.Add(request.Scenes!);
        }
        return args;
    }

    /// <summary>
    /// Select the renderers that should appear as tabs, in stable (ordinal-by-name) order: those that
    /// publish a frame buffer (<paramref name="hasFrames"/>) and speak the host's tab protocol version.
    /// A renderer that publishes frames but advertises a different protocol version is reported via
    /// <paramref name="onIncompatible"/> (so the host can warn) and excluded — the host never opens its
    /// buffer, avoiding a layout-drift corruption.
    /// </summary>
    public static List<string> SelectTabs(
        IEnumerable<InstanceInfo> instances,
        Func<string, bool> hasFrames,
        int hostProtocolVersion,
        Action<InstanceInfo>? onIncompatible = null)
    {
        var tabs = new List<string>();
        foreach (var info in instances)
        {
            if (!hasFrames(info.Name))
            {
                continue;
            }
            if (info.ProtocolVersion != hostProtocolVersion)
            {
                onIncompatible?.Invoke(info);
                continue;
            }
            tabs.Add(info.Name);
        }
        tabs.Sort(StringComparer.Ordinal);
        return tabs;
    }

    /// <summary>
    /// The other host whose <b>tab strip band</b> the global cursor is over — or within
    /// <paramref name="margin"/> of (so a drag merely <i>near</i> the tabs still resolves a merge target,
    /// Chrome-style) — or null. Body hits still don't count. Used both per-frame (show the live merge
    /// preview on that host vs. the floating thumbnail) and on release (merge only when over/near a strip —
    /// a drop over a window body does not merge). <paramref name="others"/> must exclude self.
    /// </summary>
    public static string? ResolveStripTarget(
        IReadOnlyList<(string Host, WindowBounds Bounds)> others, float cursorX, float cursorY, float margin = 0f)
    {
        foreach (var (host, b) in others)
        {
            if (b.ContainsStripWithin(cursorX, cursorY, margin))
            {
                return host;
            }
        }
        return null;
    }

    private static string? GetString(JsonElement obj, string property) =>
        obj.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
}
