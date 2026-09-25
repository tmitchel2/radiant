namespace Radiant.Host.Ipc.Recents;

/// <summary>
/// One entry in the "recently opened" list: an absolute file or folder path and the UTC time it was
/// last opened. Ordered most-recently-opened-first by <see cref="RecentFilesStore"/>.
/// </summary>
public sealed record RecentEntry(string Path, DateTime OpenedAtUtc);
