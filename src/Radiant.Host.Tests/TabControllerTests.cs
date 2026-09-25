using System.Text.Json;
using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host.Tests;

[TestClass]
public sealed class TabControllerTests
{
    private static readonly string[] s_closed = ["a"];
    private static readonly string[] s_remaining = ["b"];
    private static readonly string[] s_bca = ["b", "c", "a"];
    private static readonly string[] s_abc = ["a", "b", "c"];
    private static readonly string[] s_cab = ["c", "a", "b"];
    private static readonly string[] s_bac = ["b", "a", "c"];
    private static readonly string[] s_bc = ["b", "c"];

    private static AgentCommand Cmd(string action, string? paramsJson = null) => new()
    {
        Id = "test",
        Action = action,
        Params = paramsJson is null ? null : JsonDocument.Parse(paramsJson).RootElement,
    };

    [TestMethod]
    public void TabListReturnsScannedTabs()
    {
        var controller = new TabController("host", scan: () => ["a", "b"]);
        var resp = controller.Handle(Cmd("tab.list"));

        Assert.AreEqual("ok", resp.Status);
        var result = resp.Result!.Value;
        Assert.AreEqual(0, result.GetProperty("activeIndex").GetInt32());
        Assert.AreEqual("a", result.GetProperty("active").GetString());
        Assert.AreEqual(2, result.GetProperty("tabs").GetArrayLength());
    }

    [TestMethod]
    public void TabActivateByNameSelectsThatTab()
    {
        var controller = new TabController("host", scan: () => ["a", "b", "c"]);
        var resp = controller.Handle(Cmd("tab.activate", """{"name":"c"}"""));

        Assert.AreEqual("ok", resp.Status);
        Assert.AreEqual("c", controller.ActiveName);
        Assert.AreEqual(2, controller.ActiveIndex);
    }

    [TestMethod]
    public void TabActivateInvalidTargetReturnsInvalidParams()
    {
        var controller = new TabController("host", scan: () => ["a"]);
        var resp = controller.Handle(Cmd("tab.activate", """{"index":9}"""));

        Assert.AreEqual("error", resp.Status);
        Assert.AreEqual("invalid_params", resp.Error!.Code);
    }

    [TestMethod]
    public void TabSpawnInvokesSpawnerAndReturnsNameAndPid()
    {
        SpawnRequest? captured = null;
        var controller = new TabController("host",
            scan: () => [],
            spawn: req => { captured = req; return (req.Name, 4242); });

        var resp = controller.Handle(Cmd("tab.spawn", """{"buildPath":"/wt/a","name":"mytab"}"""));

        Assert.AreEqual("ok", resp.Status);
        Assert.IsNotNull(captured);
        Assert.AreEqual("/wt/a", captured!.BuildPath);
        Assert.AreEqual("mytab", resp.Result!.Value.GetProperty("name").GetString());
        Assert.AreEqual(4242, resp.Result!.Value.GetProperty("pid").GetInt32());
    }

    [TestMethod]
    public void TabSpawnWithoutBuildPathReturnsInvalidParams()
    {
        var controller = new TabController("host", scan: () => [], spawn: _ => ("x", 1));
        var resp = controller.Handle(Cmd("tab.spawn", """{}"""));

        Assert.AreEqual("error", resp.Status);
        Assert.AreEqual("invalid_params", resp.Error!.Code);
    }

    [TestMethod]
    public void TabCloseInvokesCloserAndRemovesTab()
    {
        var closed = new List<string>();
        var controller = new TabController("host",
            scan: () => ["a", "b"],
            close: name => { closed.Add(name); return true; });

        var resp = controller.Handle(Cmd("tab.close", """{"name":"a"}"""));

        Assert.AreEqual("ok", resp.Status);
        CollectionAssert.AreEqual(s_closed, closed);
        // After close, the in-memory tab set drops it immediately (a rescan reconciles later).
        CollectionAssert.AreEqual(s_remaining, resp.Result!.Value
            .GetProperty("tabs").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToArray());
    }

    [TestMethod]
    public void SpawnLocalTabInvokesLocalSpawnerWithGeneratedName()
    {
        // The strip's + button calls SpawnLocalTab(), which generates a name and runs the local-spawn seam.
        string? spawnedName = null;
        string? spawnedScene = null;
        var controller = new TabController("host",
            scan: () => [],
            spawnLocal: (name, scene) => { spawnedName = name; spawnedScene = scene; return (name, 1234); });

        var (name, pid) = controller.SpawnLocalTab();

        Assert.IsNotNull(spawnedName);
        Assert.AreEqual(spawnedName, name);
        Assert.AreEqual(1234, pid);
        Assert.IsNull(spawnedScene); // no scene path for the + button
        StringAssert.StartsWith(name, "tab-", StringComparison.Ordinal);
    }

    [TestMethod]
    public void SpawnLocalTabForwardsScenePathToLocalSpawner()
    {
        // File ▸ New / Open / Open Recent call SpawnLocalTab(path); the path must reach the spawn seam
        // (which forwards it to the renderer as --scene so the new tab boots into that scene).
        string? spawnedScene = null;
        var controller = new TabController("host",
            scan: () => [],
            spawnLocal: (name, scene) => { spawnedScene = scene; return (name, 1234); });

        controller.SpawnLocalTab("/scenes/airfoil.csx");

        Assert.AreEqual("/scenes/airfoil.csx", spawnedScene);
    }

    [TestMethod]
    public void CloseTabByIndexInvokesCloserAndRemovesTab()
    {
        // The strip's close (×) button calls CloseTab(index) directly (not via the action layer).
        var closed = new List<string>();
        var controller = new TabController("host",
            scan: () => ["a", "b"],
            close: name => { closed.Add(name); return true; });
        controller.Refresh();
        controller.Activate(1); // active = "b"

        Assert.IsTrue(controller.CloseTab(1));
        CollectionAssert.AreEqual(s_remaining, closed.ToArray()); // closed "b"
        CollectionAssert.AreEqual(s_closed, controller.Tabs.ToArray()); // "a" remains
        Assert.AreEqual("a", controller.ActiveName);
    }

    [TestMethod]
    public void CloseTabOutOfRangeIsNoOp()
    {
        var controller = new TabController("host", scan: () => ["a"], close: _ => true);
        controller.Refresh();
        Assert.IsFalse(controller.CloseTab(5));
        Assert.IsFalse(controller.CloseTab(-1));
    }

    [TestMethod]
    public void TabDetachInvokesDetachAndDropsTab()
    {
        string? detached = null;
        var controller = new TabController("host",
            scan: () => ["a", "b"],
            detach: (name, _) => { detached = name; return ("radiant-host-99", 7777); });

        var resp = controller.Handle(Cmd("tab.detach", """{"name":"a"}"""));

        Assert.AreEqual("ok", resp.Status);
        Assert.AreEqual("a", detached);
        Assert.AreEqual("radiant-host-99", resp.Result!.Value.GetProperty("name").GetString());
        Assert.AreEqual(7777, resp.Result!.Value.GetProperty("pid").GetInt32());
        CollectionAssert.AreEqual(s_remaining, controller.Tabs.ToArray());
    }

    [TestMethod]
    public void LiveDetachPassesFollowFlagToSeam()
    {
        bool? followSeen = null;
        var controller = new TabController("host",
            scan: () => ["a", "b"],
            detach: (_, follow) => { followSeen = follow; return ("radiant-host-99", 7777); });
        controller.Refresh();

        controller.DetachTab(0, follow: true);
        Assert.IsTrue(followSeen);
    }

    [TestMethod]
    public void HandoffInvokesSeamAndForgetsTabOnSuccess()
    {
        (string Name, string Target, float? CursorX)? handed = null;
        var controller = new TabController("host",
            scan: () => ["a", "b"],
            handoff: (name, target, cursorX) => { handed = (name, target, cursorX); return true; });
        controller.Refresh();

        var resp = controller.Handle(Cmd("tab.handoff", """{"name":"a","target":"radiant-host-2","cursorX":50.0}"""));

        Assert.AreEqual("ok", resp.Status);
        Assert.IsNotNull(handed);
        Assert.AreEqual("a", handed!.Value.Name);
        Assert.AreEqual("radiant-host-2", handed.Value.Target);
        Assert.AreEqual(50f, handed.Value.CursorX!.Value, 1e-4f);
        // The handed-off tab is dropped locally (so a now-empty follower self-closes).
        CollectionAssert.AreEqual(s_remaining, controller.Tabs.ToArray());
    }

    [TestMethod]
    public void HandoffKeepsTabWhenSeamFails()
    {
        var controller = new TabController("host",
            scan: () => ["a", "b"],
            handoff: (_, _, _) => false); // target unreachable / refused
        controller.Refresh();

        controller.Handle(Cmd("tab.handoff", """{"name":"a","target":"dead-host"}"""));

        CollectionAssert.AreEqual(s_abc[..2], controller.Tabs.ToArray()); // ["a","b"] kept
    }

    [TestMethod]
    public void HandoffWithoutTargetReturnsInvalidParams()
    {
        var controller = new TabController("host", scan: () => ["a"], handoff: (_, _, _) => true);
        var resp = controller.Handle(Cmd("tab.handoff", """{"name":"a"}"""));

        Assert.AreEqual("error", resp.Status);
        Assert.AreEqual("invalid_params", resp.Error!.Code);
    }

    [TestMethod]
    public void DetachOfSoleTabIsRejected()
    {
        // Invariant: a tear-off must never empty its source host. Detaching the only tab is refused (it is
        // already in its own window) — the sole-tab drag gesture moves the whole window instead.
        var detachCalled = false;
        var controller = new TabController("host",
            scan: () => ["a"],
            detach: (name, _) => { detachCalled = true; return ("radiant-host-99", 7777); });

        var resp = controller.Handle(Cmd("tab.detach", """{"name":"a"}"""));

        Assert.AreEqual("error", resp.Status);
        Assert.IsFalse(detachCalled, "the detach seam must not run for a sole tab");
        CollectionAssert.AreEqual(s_closed, controller.Tabs.ToArray());
    }

    [TestMethod]
    public void DetachTabThrowsWhenItWouldEmptyHost()
    {
        var controller = new TabController("host", scan: () => ["a"]);
        controller.Refresh();
        Assert.ThrowsExactly<InvalidOperationException>(() => controller.DetachTab(0));
    }

    [TestMethod]
    public void UnknownActionReturnsNotFound()
    {
        var controller = new TabController("host", scan: () => []);
        var resp = controller.Handle(Cmd("tab.bogus"));

        Assert.AreEqual("error", resp.Status);
        Assert.AreEqual("not_found", resp.Error!.Code);
    }

    [TestMethod]
    public void RefreshPreservesActiveTabByName()
    {
        var live = new List<string> { "a", "b", "c" };
        var controller = new TabController("host", scan: () => [.. live]);
        controller.Refresh();
        controller.Activate(2); // active = "c"

        live.Remove("a"); // "a" closes; list becomes ["b", "c"]
        controller.Refresh();

        Assert.AreEqual("c", controller.ActiveName);
        Assert.AreEqual(1, controller.ActiveIndex);
    }

    [TestMethod]
    public void ReorderMovesTabPreservingActiveByName()
    {
        var controller = new TabController("host", scan: () => ["a", "b", "c"]);
        controller.Refresh();
        controller.Activate(0); // active = "a"

        Assert.IsTrue(controller.Reorder(0, 3)); // move "a" to the end
        CollectionAssert.AreEqual(s_bca, controller.Tabs.ToArray());
        Assert.AreEqual("a", controller.ActiveName);
        Assert.AreEqual(2, controller.ActiveIndex);
    }

    [TestMethod]
    public void ReorderToSameSlotIsNoOp()
    {
        var controller = new TabController("host", scan: () => ["a", "b", "c"]);
        controller.Refresh();
        Assert.IsFalse(controller.Reorder(0, 0));
        Assert.IsFalse(controller.Reorder(0, 1)); // gap immediately after itself
        CollectionAssert.AreEqual(s_abc, controller.Tabs.ToArray());
    }

    [TestMethod]
    public void RefreshPreservesManualOrder()
    {
        var live = new List<string> { "a", "b", "c" };
        var controller = new TabController("host", scan: () => [.. live]);
        controller.Refresh();
        controller.Reorder(2, 0); // → ["c", "a", "b"]

        controller.Refresh(); // same live set, sorted; manual order must survive
        CollectionAssert.AreEqual(s_cab, controller.Tabs.ToArray());
    }

    [TestMethod]
    public void RefreshAppendsNewcomersKeepingExistingOrder()
    {
        var live = new List<string> { "a", "b" };
        var controller = new TabController("host", scan: () => [.. live]);
        controller.Refresh();
        controller.Reorder(1, 0); // → ["b", "a"]

        live.Add("c"); // a new renderer attaches
        controller.Refresh();
        CollectionAssert.AreEqual(s_bac, controller.Tabs.ToArray());
    }

    [TestMethod]
    public void AdoptTabTakesOwnershipInsertsAndActivates()
    {
        string? adopted = null;
        var controller = new TabController("host",
            scan: () => ["a", "b"],
            adopt: name => { adopted = name; return true; });
        controller.Refresh();

        controller.AdoptTab("c", cursorX: null); // no bounds in tests → append

        Assert.AreEqual("c", adopted);
        CollectionAssert.AreEqual(s_abc, controller.Tabs.ToArray());
        Assert.AreEqual("c", controller.ActiveName);
    }

    [TestMethod]
    public void TabAdoptActionTakesOwnershipAndActivates()
    {
        string? adopted = null;
        var controller = new TabController("host",
            scan: () => ["a"],
            adopt: name => { adopted = name; return true; });

        var resp = controller.Handle(Cmd("tab.adopt", """{"name":"b"}"""));

        Assert.AreEqual("ok", resp.Status);
        Assert.AreEqual("b", adopted);
        Assert.AreEqual("b", controller.ActiveName);
    }

    [TestMethod]
    public void TabAdoptWithoutNameReturnsInvalidParams()
    {
        var controller = new TabController("host", scan: () => [], adopt: _ => true);
        var resp = controller.Handle(Cmd("tab.adopt", """{}"""));

        Assert.AreEqual("error", resp.Status);
        Assert.AreEqual("invalid_params", resp.Error!.Code);
    }

    [TestMethod]
    public void ForgetRemovesTabLocallyPreservingActive()
    {
        var controller = new TabController("host", scan: () => ["a", "b", "c"]);
        controller.Refresh();
        controller.Activate(2); // active = "c"

        Assert.IsTrue(controller.Forget(0)); // drop "a" (source side of a merge)
        CollectionAssert.AreEqual(s_bc, controller.Tabs.ToArray());
        Assert.AreEqual("c", controller.ActiveName);
    }

    [TestMethod]
    public void ActionsListReturnsTabActions()
    {
        var controller = new TabController("host", scan: () => []);
        var resp = controller.Handle(Cmd("actions.list"));

        Assert.AreEqual("ok", resp.Status);
        var names = resp.Result!.Value.EnumerateArray().Select(d => d.GetProperty("name").GetString()).ToArray();
        CollectionAssert.Contains(names, "tab.spawn");
        CollectionAssert.Contains(names, "window.focus");
    }

    [TestMethod]
    public void WindowFocusInvokesFocusSeam()
    {
        var focused = 0;
        var controller = new TabController("host", scan: () => ["a"], focusWindow: () => focused++);

        var resp = controller.Handle(Cmd("window.focus"));

        Assert.AreEqual("ok", resp.Status);
        Assert.AreEqual(1, focused);
    }

    [TestMethod]
    public void WindowFocusWithoutSeamReturnsUnsupported()
    {
        var controller = new TabController("host", scan: () => ["a"]);

        var resp = controller.Handle(Cmd("window.focus"));

        Assert.AreEqual("error", resp.Status);
        Assert.AreEqual("unsupported", resp.Error!.Code);
    }
}
