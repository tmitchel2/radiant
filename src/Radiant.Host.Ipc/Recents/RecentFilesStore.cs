using System.Text.Json;

namespace Radiant.Host.Ipc.Recents;

/// <summary>
/// A small persisted MRU (most-recently-used) list of scene files and folders the user has opened,
/// shared across the host and renderer processes via <c>&lt;data directory&gt;/recent.json</c>. The host's native
/// "File ▸ Open Recent" menu reads it; both the host (on dialog-open) and the renderer (on in-window
/// scene-open) write it.
///
/// <para>"Recent" means recently <em>opened</em>, so ordering is by the stored <see cref="RecentEntry.OpenedAtUtc"/>
/// — not file modification time. Writes are atomic (temp-file + <see cref="File.Move(string,string,bool)"/>);
/// two processes writing concurrently is last-writer-wins, which is acceptable for an MRU list. Every
/// operation is exception-tolerant: a missing/malformed file yields an empty list and a failed write is
/// swallowed, so menu/UI code never has to guard against I/O faults.</para>
/// </summary>
public static class RecentFilesStore
{
    /// <summary>Maximum number of entries retained (the spec's "top 10").</summary>
    public const int MaxEntries = 10;

    /// <summary>
    /// Directory holding <c>recent.json</c>: the application's data directory, the shared base the rest of
    /// the cross-process IPC uses. An application sets it through <c>RadiantAppIdentity.Use</c>; tests point
    /// it at a temp directory. Defaults to <c>~/.radiant</c>.
    /// </summary>
    public static string StoreDir { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".radiant");

    private static string FilePath => Path.Combine(StoreDir, "recent.json");

    /// <summary>
    /// Records <paramref name="path"/> as the most-recently-opened entry: normalised to an absolute path,
    /// deduplicated case-insensitively (an existing entry moves to the front), capped at
    /// <see cref="MaxEntries"/>. No-op (swallowed) on any I/O error or blank input.
    /// </summary>
    public static void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
#pragma warning disable CA1031 // MRU bookkeeping must never throw into UI / menu code.
        try
        {
            var full = Path.GetFullPath(path);
            var entries = Load()
                .Where(e => !string.Equals(e.Path, full, StringComparison.OrdinalIgnoreCase))
                .ToList();
            entries.Insert(0, new RecentEntry(full, DateTime.UtcNow));
            if (entries.Count > MaxEntries)
                entries = entries.GetRange(0, MaxEntries);
            Save(entries);
        }
        catch
        {
            // Best-effort: a recents write failing is not worth surfacing.
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Returns up to <paramref name="n"/> entries, most-recently-opened first, dropping any whose path no
    /// longer exists on disk. Empty list if the store is missing or unreadable.
    /// </summary>
    public static IReadOnlyList<RecentEntry> ListRecent(int n = MaxEntries) =>
        [.. Load()
            .Where(e => File.Exists(e.Path) || Directory.Exists(e.Path))
            .OrderByDescending(e => e.OpenedAtUtc)
            .Take(n)];

    private static List<RecentEntry> Load()
    {
#pragma warning disable CA1031 // A corrupt/partial file degrades to "no recents", never a crash.
        try
        {
            if (!File.Exists(FilePath)) return [];
            var json = File.ReadAllBytes(FilePath);
            var parsed = JsonSerializer.Deserialize(json, RecentsJsonContext.Default.RecentEntryArray);
            return parsed is null ? [] : [.. parsed];
        }
        catch
        {
            return [];
        }
#pragma warning restore CA1031
    }

    private static void Save(List<RecentEntry> entries)
    {
        Directory.CreateDirectory(StoreDir);
        RecentEntry[] array = [.. entries];
        var json = JsonSerializer.Serialize(array, RecentsJsonContext.Default.RecentEntryArray);
        var tmp = Path.Combine(StoreDir, $".recent.json.{Environment.ProcessId}.tmp");
        File.WriteAllText(tmp, json);
        File.Move(tmp, FilePath, overwrite: true);
    }
}
