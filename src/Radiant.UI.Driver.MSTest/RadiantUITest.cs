using Radiant.Host.AgentControlProtocol;

namespace Radiant.UI.Driver.MSTest;

/// <summary>
/// A base for MSTest classes that drive a Radiant UI: set <see cref="Driver"/> (with
/// <see cref="Use"/>) in a test or its initializer, and after each test it's disposed, having first, if
/// the test failed, saved a screenshot (where there's a GPU) and the interaction log with the test's results.
/// </summary>
public abstract class RadiantUITest
{
    private AppDriver? _driver;

    /// <summary>MSTest's context, set by MSTest.</summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>The driver this test uses.</summary>
    protected AppDriver Driver => _driver ?? throw new InvalidOperationException("No driver yet: call Use(AppDriver.InProcess(...)) or LaunchAsync first.");

    /// <summary>Makes <paramref name="driver"/> this test's, to be cleaned up after it.</summary>
    protected AppDriver Use(AppDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);
        _driver = driver;
        return driver;
    }

    /// <summary>Launches an app for this test.</summary>
    protected async Task<AppDriver> LaunchAsync(AgentLaunchOptions options, AgentTransport transport = AgentTransport.Auto) =>
        Use(await AppDriver.LaunchAsync(options, transport, TestContext.CancellationToken));

    /// <summary>After each test: keeps what shows what happened if it failed, then ends the app.</summary>
    [TestCleanup]
    public async Task CleanUpRadiantDriverAsync()
    {
        if (_driver is not { } driver)
        {
            return;
        }
        _driver = null;
        try
        {
            if (TestContext.CurrentTestOutcome != UnitTestOutcome.Passed)
            {
                await KeepEvidenceAsync(driver);
            }
        }
        finally
        {
            await driver.DisposeAsync();
        }
    }

    private async Task KeepEvidenceAsync(AppDriver driver)
    {
        var directory = Path.Combine(TestContext.TestRunResultsDirectory ?? Path.GetTempPath(), "radiant-ui", TestContext.TestName ?? "test");
        Directory.CreateDirectory(directory);
        try
        {
            var log = Path.Combine(directory, "interaction.log");
            var entries = await driver.LogTailAsync(500);
            await File.WriteAllLinesAsync(log, entries.Select(LogFormatter.Format));
            await File.WriteAllLinesAsync(Path.ChangeExtension(log, ".jsonl"), entries.Select(LogFormatter.ToJsonLine));
            TestContext.AddResultFile(log);
        }
        catch (AppDriverException e)
        {
            TestContext.WriteLine($"Couldn't read the interaction log: {e.Message}");
        }
        try
        {
            var shot = await driver.ScreenshotAsync(Path.Combine(directory, "failure.png"), "interactive");
            TestContext.AddResultFile(shot.Path);
        }
        catch (AppDriverException e) when (e.Code == AgentErrorCodes.Unsupported)
        {
            // No GPU here: the log will have to do.
        }
    }
}
