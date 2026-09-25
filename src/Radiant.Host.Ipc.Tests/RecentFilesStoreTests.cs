using Radiant.Host.Ipc.Recents;

namespace Radiant.Host.Ipc.Tests;

[TestClass]
[DoNotParallelize]
public sealed class RecentFilesStoreTests
{
    private string _testDir = null!;
    private string _savedDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _savedDir = RecentFilesStore.StoreDir;
        _testDir = Path.Combine(Path.GetTempPath(), $"radiant-recent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        RecentFilesStore.StoreDir = _testDir;
    }

    [TestCleanup]
    public void Cleanup()
    {
        RecentFilesStore.StoreDir = _savedDir;
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    // Creates a real file under the test dir so ListRecent's existence filter keeps it.
    private string TouchFile(string name)
    {
        var path = Path.Combine(_testDir, name);
        File.WriteAllText(path, "// scene");
        return path;
    }

    [TestMethod]
    public void ListRecentToleratesMissingFile()
    {
        Assert.AreEqual(0, RecentFilesStore.ListRecent().Count);
    }

    [TestMethod]
    public void AddThenListReturnsTheEntry()
    {
        var path = TouchFile("a.csx");
        RecentFilesStore.Add(path);

        var recent = RecentFilesStore.ListRecent();
        Assert.AreEqual(1, recent.Count);
        Assert.AreEqual(Path.GetFullPath(path), recent[0].Path);
    }

    [TestMethod]
    public void AddMovesExistingPathToFront()
    {
        var a = TouchFile("a.csx");
        var b = TouchFile("b.csx");
        RecentFilesStore.Add(a);
        RecentFilesStore.Add(b);
        RecentFilesStore.Add(a); // re-open a — should jump to front, not duplicate

        var recent = RecentFilesStore.ListRecent();
        Assert.AreEqual(2, recent.Count);
        Assert.AreEqual(Path.GetFullPath(a), recent[0].Path);
        Assert.AreEqual(Path.GetFullPath(b), recent[1].Path);
    }

    [TestMethod]
    public void AddDeduplicatesCaseInsensitively()
    {
        var path = TouchFile("Scene.csx");
        RecentFilesStore.Add(path);
        RecentFilesStore.Add(path.ToUpperInvariant());

        Assert.AreEqual(1, RecentFilesStore.ListRecent().Count);
    }

    [TestMethod]
    public void AddCapsAtTen()
    {
        for (var i = 0; i < 15; i++)
            RecentFilesStore.Add(TouchFile($"s{i}.csx"));

        var recent = RecentFilesStore.ListRecent(20);
        Assert.AreEqual(RecentFilesStore.MaxEntries, recent.Count);
        // The last 10 added (s5..s14) survive; the most recent (s14) is first.
        Assert.AreEqual(Path.GetFullPath(Path.Combine(_testDir, "s14.csx")), recent[0].Path);
    }

    [TestMethod]
    public void ListRecentReturnsMostRecentFirst()
    {
        var a = TouchFile("a.csx");
        var b = TouchFile("b.csx");
        var c = TouchFile("c.csx");
        RecentFilesStore.Add(a);
        RecentFilesStore.Add(b);
        RecentFilesStore.Add(c);

        var recent = RecentFilesStore.ListRecent();
        CollectionAssert.AreEqual(
            new[] { Path.GetFullPath(c), Path.GetFullPath(b), Path.GetFullPath(a) },
            recent.Select(e => e.Path).ToArray());
    }

    [TestMethod]
    public void ListRecentDropsNonexistentPaths()
    {
        var keep = TouchFile("keep.csx");
        var gone = Path.Combine(_testDir, "gone.csx");
        RecentFilesStore.Add(gone);
        RecentFilesStore.Add(keep);

        var recent = RecentFilesStore.ListRecent();
        Assert.AreEqual(1, recent.Count);
        Assert.AreEqual(Path.GetFullPath(keep), recent[0].Path);
    }

    [TestMethod]
    public void AddRetainsFolderEntries()
    {
        var folder = Path.Combine(_testDir, "scenes-subdir");
        Directory.CreateDirectory(folder);
        RecentFilesStore.Add(folder);

        var recent = RecentFilesStore.ListRecent();
        Assert.AreEqual(1, recent.Count);
        Assert.AreEqual(Path.GetFullPath(folder), recent[0].Path);
    }

    [TestMethod]
    public void AddIgnoresBlankInput()
    {
        RecentFilesStore.Add("");
        RecentFilesStore.Add("   ");
        Assert.AreEqual(0, RecentFilesStore.ListRecent().Count);
    }

    [TestMethod]
    public void ListRecentToleratesMalformedFile()
    {
        File.WriteAllText(Path.Combine(_testDir, "recent.json"), "{ not valid json");
        Assert.AreEqual(0, RecentFilesStore.ListRecent().Count);
    }

    // Guards the on-disk contract after the store moved out of the agent-protocol assembly into its own
    // RecentsJsonContext: this literal is exactly what the previous serializer wrote, so an options drift
    // (camelCase in particular — source-gen applies the naming policy on read too) would show up here
    // rather than as a silently empty Open Recent menu on every existing install.
    [TestMethod]
    public void ReadsRecentJsonWrittenBeforeTheStoreMoved()
    {
        var kept = TouchFile("kept.csx");
        File.WriteAllText(
            Path.Combine(_testDir, "recent.json"),
            $$"""
            [
              {
                "path": "{{kept.Replace("\\", "\\\\", StringComparison.Ordinal)}}",
                "openedAtUtc": "2026-08-30T12:09:55.433902Z"
              }
            ]
            """);

        var recent = RecentFilesStore.ListRecent();
        Assert.AreEqual(1, recent.Count);
        Assert.AreEqual(kept, recent[0].Path);
        // .433902s of sub-second precision must survive the round-trip, not just the whole second.
        Assert.AreEqual(
            new DateTime(2026, 8, 30, 12, 9, 55, DateTimeKind.Utc).AddTicks(4339020),
            recent[0].OpenedAtUtc.ToUniversalTime());
    }
}
