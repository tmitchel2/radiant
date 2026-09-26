using Radiant.AgentCli;

namespace Radiant.UI.Automation.Tests;

/// <summary><c>radiant-agent</c> against a served app, as an agent would use it.</summary>
[TestClass]
[DoNotParallelize]
public sealed class CliTests
{
    private static LiveApp s_app = null!;

    [ClassInitialize]
    public static async Task Start(TestContext context)
    {
        _ = context;
        s_app = await LiveApp.StartAsync(FormApp.Themed());
    }

    [ClassCleanup]
    public static async Task Stop() => await s_app.DisposeAsync();

    private static async Task<(int Code, string Out, string Error)> Run(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = await Cli.RunAsync([.. args, "-i", s_app.Name], stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    [TestMethod]
    public async Task ListsInstances()
    {
        var (code, output, _) = await Run("ls");

        Assert.AreEqual(0, code);
        StringAssert.Contains(output, s_app.Name);
        StringAssert.Contains(output, "headless");
    }

    [TestMethod]
    public async Task TheTreeIsOneNodeALine()
    {
        var (code, output, _) = await Run("tree");

        Assert.AreEqual(0, code);
        StringAssert.Matches(output, new System.Text.RegularExpressions.Regex(@"#\d+ button ""Save"" @save \(\d+,\d+ \d+×\d+\) focusable"));
        Assert.IsFalse(output.Contains("@row-45", StringComparison.Ordinal), "out of view");
    }

    [TestMethod]
    public async Task FindSaysWhereToTap()
    {
        var (code, output, error) = await Run("find", "@save");

        Assert.AreEqual(0, code);
        StringAssert.Contains(output, "tap(");
        StringAssert.Contains(error, "1 match");
    }

    [TestMethod]
    public async Task InspectWritesJsonWithTheFieldsAsked()
    {
        var (code, output, _) = await Run("inspect", "@list", "--fields", "testId,scroll");

        Assert.AreEqual(0, code);
        using var node = System.Text.Json.JsonDocument.Parse(output);
        Assert.AreEqual("list", node.RootElement.GetProperty("testId").GetString());
        Assert.IsTrue(node.RootElement.GetProperty("scroll").GetProperty("canScroll").GetProperty("y").GetBoolean());
        Assert.IsFalse(node.RootElement.TryGetProperty("bounds", out _));
    }

    [TestMethod]
    public async Task ActionsSayWhatTheyDid()
    {
        var (code, output, _) = await Run("tap", "@agree");
        var (waited, _, _) = await Run("wait", "@agree", "--checked");
        var (unchecked_, _, _) = await Run("tap", "@agree", "--transport", "file");

        Assert.AreEqual(0, code);
        StringAssert.Contains(output, "ok ui.tap");
        StringAssert.Contains(output, "@agree");
        Assert.AreEqual(0, waited);
        Assert.AreEqual(0, unchecked_);
    }

    [TestMethod]
    public async Task FailuresSayWhyAndExitNonZero()
    {
        var (code, _, error) = await Run("tap", "@sav", "--timeout", "200ms");
        var (timedOut, _, _) = await Run("wait", "@never", "--timeout", "100ms");
        var (usage, _, usageError) = await Run("tap");
        var (unknown, _, _) = await Cli.RunAsync(["tap", "@save", "-i", "no-such-app"], TextWriter.Null, TextWriter.Null) is var c ? (c, "", "") : default;

        Assert.AreEqual(1, code);
        StringAssert.Contains(error, "error no_match");
        StringAssert.Contains(error, "@save");
        Assert.AreEqual(2, timedOut);
        Assert.AreEqual(64, usage);
        StringAssert.Contains(usageError, "Missing a selector");
        Assert.AreEqual(3, unknown);
    }

    [TestMethod]
    public async Task TheLogReadsBack()
    {
        await Run("tap", "@save");
        await Run("log", "note", "hello", "from", "the", "cli");

        var (code, output, _) = await Run("log", "--src", "agent");

        Assert.AreEqual(0, code);
        StringAssert.Contains(output, "ui.tap");
        StringAssert.Contains(output, "hello from the cli");
    }
}
