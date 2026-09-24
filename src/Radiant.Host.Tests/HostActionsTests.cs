using System.Text.Json;
using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host.Tests;

[TestClass]
public sealed class HostActionsTests
{
    private static readonly string s_appProject = Path.Combine("src", "App", "App.csproj");

    private static readonly string[] s_attachArgs = ["run", "--project", "/wt/a/src/App", "--", "--attach", "--name", "t1"];
    private static readonly string[] s_selectExpected = ["a", "b"];
    private static readonly string[] s_selectIncompatible = ["oldbuild"];

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement;

    [TestMethod]
    public void ResolveTabIndexByIndexReturnsThatIndex()
    {
        var tabs = new[] { "a", "b", "c" };
        Assert.AreEqual(1, HostActions.ResolveTabIndex(tabs, Json("""{"index":1}""")));
    }

    [TestMethod]
    public void ResolveTabIndexByNameReturnsMatchingIndex()
    {
        var tabs = new[] { "a", "b", "c" };
        Assert.AreEqual(2, HostActions.ResolveTabIndex(tabs, Json("""{"name":"c"}""")));
    }

    [TestMethod]
    public void ResolveTabIndexOutOfRangeThrows()
    {
        var tabs = new[] { "a" };
        Assert.ThrowsExactly<ArgumentException>(() => HostActions.ResolveTabIndex(tabs, Json("""{"index":5}""")));
    }

    [TestMethod]
    public void ResolveTabIndexUnknownNameThrows()
    {
        var tabs = new[] { "a" };
        Assert.ThrowsExactly<ArgumentException>(() => HostActions.ResolveTabIndex(tabs, Json("""{"name":"z"}""")));
    }

    [TestMethod]
    public void ResolveTabIndexMissingParamThrows()
    {
        var tabs = new[] { "a" };
        Assert.ThrowsExactly<ArgumentException>(() => HostActions.ResolveTabIndex(tabs, Json("""{}""")));
    }

    [TestMethod]
    public void ParseSpawnRequestRequiresBuildPath()
    {
        Assert.ThrowsExactly<ArgumentException>(() => HostActions.ParseSpawnRequest(Json("""{}"""), () => "fallback"));
    }

    [TestMethod]
    public void ParseSpawnRequestUsesDefaultNameWhenAbsent()
    {
        var req = HostActions.ParseSpawnRequest(Json("""{"buildPath":"/wt/a"}"""), () => "tab-7");
        Assert.AreEqual("/wt/a", req.BuildPath);
        Assert.AreEqual("tab-7", req.Name);
        Assert.IsNull(req.Scenes);
    }

    [TestMethod]
    public void ParseSpawnRequestAcceptsProjectAndSceneAliases()
    {
        var req = HostActions.ParseSpawnRequest(Json("""{"project":"/wt/b","scene":"/scenes"}"""), () => "x");
        Assert.AreEqual("/wt/b", req.BuildPath);
        Assert.AreEqual("/scenes", req.Scenes);
    }

    [TestMethod]
    public void ResolveProjectArgPassesThroughCsproj()
    {
        Assert.AreEqual("/p/Foo.csproj", HostActions.ResolveProjectArg("/p/Foo.csproj", s_appProject, _ => false));
    }

    [TestMethod]
    public void ResolveProjectArgResolvesWorktreeRoot()
    {
        var root = Path.Combine("wt", "a");
        var worktreeProj = Path.Combine(root, s_appProject);
        var resolved = HostActions.ResolveProjectArg(root, s_appProject, p => p == worktreeProj);
        Assert.AreEqual(Path.Combine(root, "src", "App"), resolved);
    }

    [TestMethod]
    public void ResolveProjectArgFallsThroughWhenNothingMatches()
    {
        Assert.AreEqual("/nope", HostActions.ResolveProjectArg("/nope", s_appProject, _ => false));
    }

    [TestMethod]
    public void ResolveProjectArgPassesThroughWithoutAWorktreeProject()
    {
        Assert.AreEqual("/wt/a", HostActions.ResolveProjectArg("/wt/a", worktreeProject: null, _ => true));
    }

    [TestMethod]
    public void BuildSpawnArgumentsIncludesAttachAndName()
    {
        var req = new SpawnRequest("/wt/a", "t1", null);
        var args = HostActions.BuildSpawnArguments(req, "/wt/a/src/App");
        CollectionAssert.AreEqual(s_attachArgs, args);
    }

    [TestMethod]
    public void BuildSpawnArgumentsAppendsScenes()
    {
        var req = new SpawnRequest("/wt/a", "t1", "/scenes");
        var args = HostActions.BuildSpawnArguments(req, "/proj");
        Assert.IsTrue(args.Contains("--scenes"));
        Assert.AreEqual("/scenes", args[^1]);
    }

    [TestMethod]
    public void SelectTabsFiltersByFramesAndVersionAndSortsOrdinal()
    {
        var instances = new[]
        {
            Info("b", protocol: 1),
            Info("a", protocol: 1),
            Info("noframes", protocol: 1),
            Info("oldbuild", protocol: 0),
        };
        var hasFrames = (string n) => n != "noframes";
        var incompatible = new List<string>();

        var tabs = HostActions.SelectTabs(instances, hasFrames, hostProtocolVersion: 1, i => incompatible.Add(i.Name));

        CollectionAssert.AreEqual(s_selectExpected, tabs);
        CollectionAssert.AreEqual(s_selectIncompatible, incompatible);
    }

    private static InstanceInfo Info(string name, int protocol) =>
        new() { Name = name, ProtocolVersion = protocol };
}
