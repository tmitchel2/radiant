using System.Globalization;
using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Core;

namespace Radiant.UI.Automation;

/// <summary>How an <see cref="AgentServer"/> serves its app.</summary>
public sealed record AgentServerOptions
{
    /// <summary>The instance's name; <c>&lt;app&gt;-&lt;pid&gt;</c> if null.</summary>
    public string? Name { get; init; }

    /// <summary>The app's name.</summary>
    public string? AppName { get; init; }

    /// <summary>Takes commands as files in the instance's directory.</summary>
    public bool File { get; init; } = true;

    /// <summary>Takes commands on a Unix domain socket.</summary>
    public bool Socket { get; init; } = true;

    /// <summary>Where the interaction log goes; the registry's <c>logs</c> directory if null.</summary>
    public string? LogPath { get; init; }

    /// <summary>Records typed text in the log; otherwise it's written as <c>•••</c>.</summary>
    public bool RecordText { get; init; } = true;

    /// <summary>Records the pointer moving onto elements, not only presses.</summary>
    public bool RecordHover { get; init; }
}

/// <summary>
/// Puts a running app on the agent protocol: registers it as an instance (<see cref="InstanceRegistry"/>),
/// answers the <c>ui.*</c>, <c>app.*</c> and <c>log.*</c> actions over the file and socket transports, and
/// writes the interaction log. Plugged in as an extension (<see cref="UIAppOptions.Extensions"/>), usually
/// by <see cref="RadiantAutomation.Configure"/>. The instance is marked ready once the tree is first laid out.
/// </summary>
public sealed class AgentServer(AgentServerOptions? options = null) : IUIAppExtension
{
    private readonly AgentServerOptions _options = options ?? new AgentServerOptions();
    private UIAppSession? _session;
    private UIAutomation? _automation;
    private InteractionLog? _log;
    private FileDropTransport? _file;
    private AgentSocketServer? _socket;
    private InstanceInfo? _info;

    /// <summary>The instance's name, once attached.</summary>
    public string? Name => _info?.Name;

    /// <summary>The automation, once attached.</summary>
    public UIAutomation? Automation => _automation;

    /// <inheritdoc/>
    public void Attach(UIAppSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        var appName = _options.AppName ?? session.Options.Title;
        var name = _options.Name ?? InstanceRegistry.GenerateName(Slug(appName));
        var logPath = _options.LogPath ?? Path.Combine(InstanceRegistry.LogsDir,
            $"{name}-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.jsonl");
        _log = new InteractionLog(new InteractionLogOptions { Path = logPath, RecordText = _options.RecordText, RecordHover = _options.RecordHover });
        _automation = new UIAutomation(session, new UIAutomationOptions { AppName = appName, InstanceName = name, Log = _log });

        var transports = new List<string>();
        if (_options.File)
        {
            transports.Add("file");
        }
        var socketPath = _options.Socket ? InstanceRegistry.GetSocketPath(name) : null;
        if (socketPath is not null)
        {
            transports.Add("socket");
        }
        _info = new InstanceInfo
        {
            Name = name,
            Pid = Environment.ProcessId,
            StartTime = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            WorkingDirectory = Environment.CurrentDirectory,
            Capabilities = ["ui", "ui.screenshot", "log", .. session.ClockMode == UIClockMode.Fixed ? new[] { "clock.fixed" } : []],
            AgentProtocolVersion = AgentProtocol.Version,
            Kind = "ui",
            AppName = appName,
            Transports = [.. transports],
            SocketPath = socketPath,
            LogPath = _log.Path,
            Headless = session.IsHeadless,
            Clock = session.ClockMode.ToString().ToLowerInvariant(),
        };
        InstanceRegistry.Register(_info);
        if (_options.File)
        {
            _file = new FileDropTransport(name, _automation.Dispatcher);
        }
        if (socketPath is not null)
        {
            _socket = new AgentSocketServer(socketPath, _automation.Dispatcher, Hello);
            _socket.ConnectionClosed += connection => session.Post(() => _log.Unsubscribe(connection));
            _socket.Start();
        }
        session.AfterUpdate += MarkReady;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_session is not null)
        {
            _session.AfterUpdate -= MarkReady;
        }
        // Let the last response (app.exit's, say) be read before the directory goes.
        _file?.WaitForResponsesDelivered(1000);
        _file?.Dispose();
        _socket?.Dispose();
        _automation?.Dispose();
        _log?.Dispose();
        if (_info is not null)
        {
            InstanceRegistry.Deregister(_info.Name);
        }
    }

    private AgentHello Hello() => new()
    {
        Instance = _info!.Name,
        App = _info.AppName,
        Capabilities = _info.Capabilities,
        Clock = _info.Clock,
        Headless = _info.Headless,
    };

    // Once the tree has been laid out, commands can find things in it.
    private void MarkReady()
    {
        if (_session is null || _info is null || _session.Root.RootNode is null)
        {
            return;
        }
        _session.AfterUpdate -= MarkReady;
        _info.Ready = true;
        InstanceRegistry.Register(_info);
    }

    private static string Slug(string name)
    {
        var slug = new string([.. name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')]).Trim('-');
        return slug.Length == 0 ? "radiant" : slug;
    }
}

/// <summary>
/// Turns on automation for an app when it's asked for: an app calls <see cref="Configure"/> on its
/// options before <see cref="RadiantUI.Run"/>, and runs as it always does unless <c>RADIANT_AGENT=1</c>
/// is set (as <c>radiant-agent launch</c> and <c>AppDriver.LaunchAsync</c> set it) or it's given
/// <c>--agent</c>.
/// </summary>
public static class RadiantAutomation
{
    /// <summary>
    /// <paramref name="options"/> with an <see cref="AgentServer"/>, if automation is asked for; otherwise
    /// unchanged. It reads:
    /// <list type="bullet">
    /// <item><c>RADIANT_AGENT=1</c> or <c>--agent</c>: turns it on.</item>
    /// <item><c>RADIANT_AGENT_NAME</c> or <c>--agent-name n</c>: the instance's name.</item>
    /// <item><c>RADIANT_AGENT_HEADLESS=1</c> or <c>--headless</c>: no window.</item>
    /// <item><c>RADIANT_AGENT_CLOCK=real|fixed</c>: the clock; fixed when headless, real otherwise.</item>
    /// <item><c>RADIANT_AGENT_SIZE=1200x800</c>, <c>RADIANT_AGENT_SCALE=2</c>: the size and, headless, the pixel scale.</item>
    /// <item><c>RADIANT_AGENT_LOG=path</c>: the interaction log.</item>
    /// <item><c>RADIANT_AGENT_TRANSPORTS=file,socket</c>: which transports.</item>
    /// <item><c>RADIANT_AGENT_LOG_TEXT=0</c>: typed text is left out of the log.</item>
    /// <item><c>RADIANT_AGENT_LOG_HOVER=1</c>: the pointer moving onto elements goes in the log.</item>
    /// <item><c>RADIANT_INSTANCES_DIR</c>: where instances register (see <see cref="InstanceRegistry.RootDir"/>).</item>
    /// </list>
    /// </summary>
    public static UIAppOptions Configure(UIAppOptions options, IReadOnlyList<string>? args = null, string? appName = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        args ??= [];
        if (!(Flag("RADIANT_AGENT") || args.Contains("--agent")))
        {
            return options;
        }
        var headless = Flag("RADIANT_AGENT_HEADLESS") || args.Contains("--headless");
        var clock = Environment.GetEnvironmentVariable("RADIANT_AGENT_CLOCK")?.ToLowerInvariant() switch
        {
            "fixed" => UIClockMode.Fixed,
            "real" => UIClockMode.Real,
            _ => headless ? UIClockMode.Fixed : UIClockMode.Real,
        };
        var (width, height) = (options.Width, options.Height);
        if (Environment.GetEnvironmentVariable("RADIANT_AGENT_SIZE") is { } size && size.Split('x', 'X') is [var w, var h]
            && int.TryParse(w, CultureInfo.InvariantCulture, out var parsedWidth) && int.TryParse(h, CultureInfo.InvariantCulture, out var parsedHeight))
        {
            (width, height) = (parsedWidth, parsedHeight);
        }
        var scale = float.TryParse(Environment.GetEnvironmentVariable("RADIANT_AGENT_SCALE"), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedScale)
            ? parsedScale
            : options.PixelScale;
        var transports = Environment.GetEnvironmentVariable("RADIANT_AGENT_TRANSPORTS")?.ToLowerInvariant() ?? "file,socket";
        var name = Environment.GetEnvironmentVariable("RADIANT_AGENT_NAME") is { Length: > 0 } fromEnvironment
            ? fromEnvironment
            : args.SkipWhile(a => a != "--agent-name").Skip(1).FirstOrDefault();
        var server = new AgentServer(new AgentServerOptions
        {
            Name = name,
            AppName = appName ?? options.Title,
            File = transports.Contains("file", StringComparison.Ordinal),
            Socket = transports.Contains("socket", StringComparison.Ordinal),
            LogPath = Environment.GetEnvironmentVariable("RADIANT_AGENT_LOG") is { Length: > 0 } log ? log : null,
            RecordText = Environment.GetEnvironmentVariable("RADIANT_AGENT_LOG_TEXT") != "0",
            RecordHover = Flag("RADIANT_AGENT_LOG_HOVER"),
        });
        return options with
        {
            Headless = headless || options.Headless,
            Clock = clock,
            Width = width,
            Height = height,
            PixelScale = scale,
            Extensions = [.. options.Extensions, server],
        };

        static bool Flag(string variable) => Environment.GetEnvironmentVariable(variable)?.ToLowerInvariant() is "1" or "true" or "yes";
    }
}
