using System.Numerics;
using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Core;
using Radiant.UI.Driver;

namespace Radiant.UI.Automation.Tests;

/// <summary>The same test, in-process and against a served app over each transport: it must mean the same.</summary>
[TestClass]
public sealed class RemoteTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("in-process")]
    [DataRow("socket")]
    [DataRow("file")]
    public async Task TheSameTestPassesEverywhere(string how)
    {
        await using var app = how == "in-process" ? null : await LiveApp.StartAsync(FormApp.Themed());
        await using var driver = how switch
        {
            "in-process" => AppDriver.InProcess(FormApp.Themed(), new InProcessOptions { Size = new Vector2(800, 700) }),
            "socket" => await AppDriver.AttachAsync(app!.Name, AgentTransport.Socket, TestContext.CancellationToken),
            _ => await AppDriver.AttachAsync(app!.Name, AgentTransport.File, TestContext.CancellationToken),
        };

        Assert.AreEqual(how, driver.Client.Transport);
        await driver.ByTestId("save").TapAsync();
        await driver.ByTestId("status").Expect().ToHaveTextAsync("Saved 1 times");
        await driver.ByTestId("name").TypeAsync("Ada");
        await driver.ByTestId("name").Expect().ToHaveValueAsync("Ada");
        await driver.ByTestId("row-45").TapAsync();
        await driver.ByTestId("picked").Expect().ToHaveTextAsync("Picked row 45");
        var node = await driver.ByTestId("save").InspectAsync("hit,scroll");
        Assert.IsTrue(node.Hittable);
        var error = await Assert.ThrowsAsync<AppDriverException>(() => driver.CallAsync("ui.tap",
            System.Text.Json.JsonDocument.Parse("""{"selector":"@sav"}""").RootElement, TimeSpan.FromMilliseconds(300)));
        Assert.AreEqual(AgentErrorCodes.NoMatch, error.Code);
    }

    [TestMethod]
    public async Task AServedAppRegistersWhatItOffers()
    {
        await using var app = await LiveApp.StartAsync(FormApp.Themed());

        var info = app.Info;

        Assert.AreEqual("ui", info.Kind);
        Assert.AreEqual(AgentProtocol.Version, info.AgentProtocolVersion);
        CollectionAssert.AreEquivalent(new[] { "file", "socket" }, info.Transports);
        Assert.IsTrue(File.Exists(info.SocketPath));
        Assert.IsTrue(info.Headless);
        Assert.AreEqual("fixed", info.Clock);
        Assert.IsNotNull(info.LogPath);
    }

    [TestMethod]
    public async Task OnTheFixedClockTimeMovesOnlyWhenAsked()
    {
        await using var app = await LiveApp.StartAsync(FormApp.Themed());
        await using var driver = await AppDriver.AttachAsync(app.Name, AgentTransport.Socket, TestContext.CancellationToken);

        var before = await driver.InfoAsync();
        await Task.Delay(200, TestContext.CancellationToken);
        var idle = await driver.InfoAsync();
        var stepped = await driver.StepAsync(TimeSpan.FromSeconds(2));

        Assert.IsTrue(idle.Time - before.Time < 0.1, "a spinner alone doesn't run frames");
        Assert.IsTrue(stepped.Time - idle.Time >= 1.99, $"{idle.Time} → {stepped.Time}");
    }

    [TestMethod]
    public async Task OnTheRealClockItRunsInRealTime()
    {
        await using var app = await LiveApp.StartAsync(FormApp.Themed(), UIClockMode.Real);
        await using var driver = await AppDriver.AttachAsync(app.Name, AgentTransport.Socket, TestContext.CancellationToken);

        await driver.ByTestId("save").TapAsync();

        await driver.ByTestId("status").Expect().ToHaveTextAsync("Saved 1 times");
        Assert.AreEqual("real", (await driver.InfoAsync()).Clock);
    }

    [TestMethod]
    public async Task TheLogStreamsOverTheSocketAndIsWrittenToItsFile()
    {
        await using var app = await LiveApp.StartAsync(FormApp.Themed());
        await using var driver = await AppDriver.AttachAsync(app.Name, AgentTransport.Socket, TestContext.CancellationToken);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var streamed = new List<LogEntry>();
        var reading = Task.Run(async () =>
        {
            await foreach (var entry in driver.LogAsync("agent", cancellation: stop.Token))
            {
                streamed.Add(entry);
                if (entry.Kind == LogKinds.Result)
                {
                    await stop.CancelAsync();
                }
            }
        }, TestContext.CancellationToken);
        await Task.Delay(100, TestContext.CancellationToken);

        await driver.ByTestId("save").TapAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => reading.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken));

        Assert.AreEqual("ui.tap", streamed[0].Action!.Name);
        var lines = await File.ReadAllLinesAsync(app.Info.LogPath!, TestContext.CancellationToken);
        Assert.IsTrue(lines.Select(LogFormatter.FromJsonLine).Any(e => e?.Kind == LogKinds.Result && e.Action?.Name == "ui.tap"));
    }

    [TestMethod]
    public async Task ADisconnectedClientsWaitIsCancelled()
    {
        await using var app = await LiveApp.StartAsync(FormApp.Themed());
        var client = await AgentClient.ConnectAsync(app.Name, AgentTransport.Socket, TestContext.CancellationToken);
        var waiting = client.SendAsync("ui.waitFor", System.Text.Json.JsonDocument.Parse("""{"selector":"@never"}""").RootElement, 30_000, TestContext.CancellationToken);

        await client.DisposeAsync();
        var response = await waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        Assert.AreEqual(AgentErrorCodes.Unreachable, response.Error!.Code);
        await using var again = await AppDriver.AttachAsync(app.Name, AgentTransport.Socket, TestContext.CancellationToken);
        await again.ByTestId("save").TapAsync();
    }
}
