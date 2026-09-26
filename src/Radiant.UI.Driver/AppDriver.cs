using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Radiant.Host.AgentControlProtocol;
using Radiant.Platform;
using Radiant.Text;
using Radiant.UI.Automation;
using Radiant.UI.Core;

namespace Radiant.UI.Driver;

/// <summary>How <see cref="AppDriver.InProcess"/> runs the app.</summary>
public sealed record InProcessOptions
{
    /// <summary>The UI's size in logical points.</summary>
    public Vector2 Size { get; init; } = new(1024, 768);

    /// <summary>Pixels per point, for screenshots.</summary>
    public float PixelScale { get; init; } = 1f;

    /// <summary>The platform; the headless one if null.</summary>
    public IPlatform? Platform { get; init; }

    /// <summary>The fonts; Radiant's own if null.</summary>
    public FontLibrary? Fonts { get; init; }

    /// <summary>The app's name, for the log.</summary>
    public string? AppName { get; init; }

    /// <summary>A file to write the interaction log to; memory only if null.</summary>
    public string? LogPath { get; init; }
}

/// <summary>
/// Drives a Radiant UI app from a test, in the style of Detox: find elements with selectors
/// (<see cref="Get(string)"/>), act on them (each action waits for the app to settle, brings the element
/// into view and presses where it can be hit), and expect things of them (each expectation waits until
/// it holds or times out). Runs the app in-process (<see cref="InProcess"/>) or talks to a real one
/// (<see cref="LaunchAsync"/>, <see cref="AttachAsync"/>); the same test works on either.
/// </summary>
public sealed class AppDriver : IAsyncDisposable
{
    private readonly LaunchedApp? _launched;

    /// <summary>A driver over <paramref name="client"/>.</summary>
    public AppDriver(IAgentClient client) : this(client, null)
    {
    }

    private AppDriver(IAgentClient client, LaunchedApp? launched)
    {
        ArgumentNullException.ThrowIfNull(client);
        Client = client;
        _launched = launched;
    }

    /// <summary>How commands reach the app.</summary>
    public IAgentClient Client { get; }

    /// <summary>The app, when it runs in this process.</summary>
    public UIAppSession? Session => (Client as InProcessClient)?.Session;

    /// <summary>The process, when this driver launched it.</summary>
    public System.Diagnostics.Process? Process => _launched?.Process;

    /// <summary>How long actions and expectations wait by default.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Runs <paramref name="root"/> in this process, headless on the fixed clock.</summary>
    public static AppDriver InProcess(Element root, InProcessOptions? options = null)
    {
        options ??= new InProcessOptions();
        var session = UIAppSession.CreateManual(root, options.Size, options.PixelScale, options.Platform, options.Fonts);
        var automation = new UIAutomation(session, new UIAutomationOptions
        {
            AppName = options.AppName,
            Log = new InteractionLog(new InteractionLogOptions { Path = options.LogPath }),
        });
        session.Step();
        return new AppDriver(new InProcessClient(automation, owns: true));
    }

    /// <summary>Starts an app (building it if it's a project) with automation on, and connects to it.</summary>
    public static async Task<AppDriver> LaunchAsync(AgentLaunchOptions options, AgentTransport transport = AgentTransport.Auto, CancellationToken cancellation = default)
    {
        var launched = await AgentLauncher.LaunchAsync(options, cancellation);
        try
        {
            var client = await AgentClient.ConnectAsync(launched.Instance.Name, transport, cancellation);
            return new AppDriver(client, launched);
        }
        catch
        {
            launched.Process.Kill(entireProcessTree: true);
            throw;
        }
    }

    /// <summary>Connects to a running app.</summary>
    public static async Task<AppDriver> AttachAsync(string? instance = null, AgentTransport transport = AgentTransport.Auto, CancellationToken cancellation = default) =>
        new(await AgentClient.ConnectAsync(AgentClient.ResolveInstance(instance), transport, cancellation));

    /// <summary>The elements <paramref name="selector"/> means, in the compact syntax (<c>@save</c>, <c>role=button label=Save</c>).</summary>
    public Locator Get(string selector) => new(this, Selector.Parse(selector));

    /// <summary>The elements <paramref name="selector"/> means.</summary>
    public Locator Get(Selector selector) => new(this, selector);

    /// <summary>The element with this test ID.</summary>
    public Locator ByTestId(string testId) => new(this, new Selector { TestId = testId });

    /// <summary>Elements showing this text (a label or a value), exactly.</summary>
    public Locator ByText(string text) => new(this, new Selector { Text = TextMatch.Exact(text) });

    /// <summary>Elements of this role, and label if given.</summary>
    public Locator ByRole(string role, string? label = null) => new(this, new Selector { Role = role, Label = label is null ? null : TextMatch.Exact(label) });

    /// <summary>Sends a command and returns its response, whatever it is.</summary>
    public Task<AgentResponse> SendAsync(string action, JsonElement? parameters = null, TimeSpan? timeout = null, CancellationToken cancellation = default) =>
        Client.SendAsync(action, parameters, (int)(timeout ?? Timeout).TotalMilliseconds, cancellation);

    /// <summary>Sends a command and returns its result; throws <see cref="AppDriverException"/> if it failed.</summary>
    public async Task<JsonElement> CallAsync(string action, JsonElement? parameters = null, TimeSpan? timeout = null, CancellationToken cancellation = default)
    {
        var response = await SendAsync(action, parameters, timeout, cancellation);
        if (response.Status == "error")
        {
            var error = response.Error ?? new AgentError { Code = AgentErrorCodes.Internal, Message = "failed" };
            throw new AppDriverException(error.Code, $"{action}: {error.Message}", error.Details, await RecentAsync());
        }
        return response.Result ?? default;
    }

    internal async Task<TResult> CallAsync<TParams, TResult>(string action, TParams parameters, JsonTypeInfo<TParams> paramsType, JsonTypeInfo<TResult> resultType, TimeSpan? timeout = null, CancellationToken cancellation = default)
    {
        var result = await CallAsync(action, JsonSerializer.SerializeToElement(parameters, paramsType), timeout, cancellation);
        return result.Deserialize(resultType) ?? throw new AppDriverException(AgentErrorCodes.ParseError, $"{action} answered nothing.");
    }

    /// <summary>What the app says about itself.</summary>
    public async Task<AppInfo> InfoAsync(CancellationToken cancellation = default) =>
        (await CallAsync("app.info", null, null, cancellation)).Deserialize(AutomationJsonContext.Default.AppInfo)!;

    /// <summary>Waits until the app is idle.</summary>
    public Task<IdleResult> WaitForIdleAsync(TimeSpan? timeout = null, CancellationToken cancellation = default) =>
        CallAsync("app.idle", new IdleParams(), AutomationJsonContext.Default.IdleParams, AutomationJsonContext.Default.IdleResult, timeout, cancellation);

    /// <summary>Runs frames.</summary>
    public Task<StepResult> StepAsync(int frames = 1, CancellationToken cancellation = default) =>
        CallAsync("app.step", new StepParams { Frames = frames }, AutomationJsonContext.Default.StepParams, AutomationJsonContext.Default.StepResult, null, cancellation);

    /// <summary>Runs frames until <paramref name="time"/> of UI time has passed: on the fixed clock, a timer's wait.</summary>
    public Task<StepResult> StepAsync(TimeSpan time, CancellationToken cancellation = default) =>
        CallAsync("app.step", new StepParams { Ms = time.TotalMilliseconds }, AutomationJsonContext.Default.StepParams, AutomationJsonContext.Default.StepResult,
            time + Timeout, cancellation);

    /// <summary>Presses a key chord, e.g. <c>"Tab"</c>, <c>"Cmd+S"</c>.</summary>
    public Task<ActionResult> KeyAsync(string chord, int repeat = 1, CancellationToken cancellation = default) =>
        CallAsync("ui.key", new KeyParams { Chord = chord, Repeat = repeat }, AutomationJsonContext.Default.KeyParams, AutomationJsonContext.Default.ActionResult, null, cancellation);

    /// <summary>Types into whatever has focus.</summary>
    public Task<ActionResult> TypeAsync(string text, CancellationToken cancellation = default) =>
        CallAsync("ui.type", new ElementActionParams { Text = text }, AutomationJsonContext.Default.ElementActionParams, AutomationJsonContext.Default.ActionResult, null, cancellation);

    /// <summary>Taps a point, in logical window points.</summary>
    public Task<ActionResult> TapAtAsync(Vector2 at, CancellationToken cancellation = default) =>
        CallAsync("ui.tapAt", new TapAtParams { X = at.X, Y = at.Y }, AutomationJsonContext.Default.TapAtParams, AutomationJsonContext.Default.ActionResult, null, cancellation);

    /// <summary>The tree, with the fields asked for (see <see cref="InspectParams.Fields"/>).</summary>
    public Task<InspectDocument> TreeAsync(string? fields = null, int depth = -1, bool visibleOnly = false, bool renderTree = false, CancellationToken cancellation = default) =>
        CallAsync("ui.tree", new TreeParams { Fields = fields, Depth = depth, VisibleOnly = visibleOnly, Tree = renderTree ? "render" : null },
            AutomationJsonContext.Default.TreeParams, AutomationJsonContext.Default.InspectDocument, null, cancellation);

    /// <summary>A screenshot, optionally annotated.</summary>
    public Task<ScreenshotInfo> ScreenshotAsync(string? path = null, string annotate = "none", Selector? crop = null, CancellationToken cancellation = default) =>
        CallAsync("ui.screenshot", new ScreenshotParams { Path = path, Annotate = annotate, Selector = crop },
            AutomationJsonContext.Default.ScreenshotParams, AutomationJsonContext.Default.ScreenshotInfo, null, cancellation);

    /// <summary>Writes a note in the interaction log.</summary>
    public Task NoteAsync(string text, CancellationToken cancellation = default) =>
        CallAsync("log.note", new LogNoteParams { Text = text }, AutomationJsonContext.Default.LogNoteParams, AutomationJsonContext.Default.LogNoteResult, null, cancellation);

    /// <summary>The last <paramref name="count"/> entries of the interaction log.</summary>
    public async Task<IReadOnlyList<LogEntry>> LogTailAsync(int count = 50, CancellationToken cancellation = default) =>
        await CallAsync("log.tail", new LogNoteParams { Count = count }, AutomationJsonContext.Default.LogNoteParams, AutomationJsonContext.Default.LogEntryArray, null, cancellation);

    /// <summary>
    /// The interaction log as it's written, from now (or from after <paramref name="since"/>), until
    /// cancelled; <paramref name="sources"/> limits it to <c>human</c>, <c>agent</c>, <c>test</c>, <c>app</c>.
    /// </summary>
    public async IAsyncEnumerable<LogEntry> LogAsync(string? sources = null, long since = -1, [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        var events = Client.Events ?? throw new AppDriverException(AgentErrorCodes.Unsupported, $"The {Client.Transport} transport can't stream the log.");
        var subscription = await CallAsync("log.subscribe", new LogSubscribeParams { Src = sources, Since = since },
            AutomationJsonContext.Default.LogSubscribeParams, AutomationJsonContext.Default.LogSubscription, null, cancellation);
        while (await events.WaitToReadAsync(cancellation))
        {
            while (events.TryRead(out var agentEvent))
            {
                if (agentEvent.Sub == subscription.Sub && agentEvent.Data is { } data && data.Deserialize(AgentJsonContext.Default.LogEntry) is { } entry)
                {
                    yield return entry;
                }
            }
        }
    }

    /// <summary>Ends the app (when this driver launched it or runs it) and the connection.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_launched is { } launched && !launched.Process.HasExited)
        {
            try
            {
                await Client.SendAsync("app.exit", timeoutMs: 2000);
                using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await launched.Process.WaitForExitAsync(wait.Token);
            }
            catch (Exception e) when (e is OperationCanceledException or InvalidOperationException)
            {
                launched.Process.Kill(entireProcessTree: true);
            }
        }
        await Client.DisposeAsync();
        _launched?.Process.Dispose();
    }

    private async Task<IReadOnlyList<LogEntry>> RecentAsync()
    {
        try
        {
            var response = await Client.SendAsync("log.tail", JsonSerializer.SerializeToElement(new LogNoteParams { Count = 12 }, AutomationJsonContext.Default.LogNoteParams), 2000);
            return response.Result?.Deserialize(AutomationJsonContext.Default.LogEntryArray) ?? [];
        }
        catch (Exception e) when (e is JsonException or IOException or InvalidOperationException)
        {
            return [];
        }
    }
}
