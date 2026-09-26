using System.Diagnostics;
using System.Globalization;

namespace Radiant.Host.AgentControlProtocol;

/// <summary>How <see cref="AgentLauncher"/> starts an app for automation.</summary>
public sealed record AgentLaunchOptions
{
    /// <summary>
    /// What to start: an executable, a <c>.dll</c> (run with <c>dotnet</c>), a <c>.csproj</c>, or a directory
    /// holding one (built first).
    /// </summary>
    public string Target { get; init; } = "";

    /// <summary>The instance's name; made up if null.</summary>
    public string? Name { get; init; }

    /// <summary>Runs without a window.</summary>
    public bool Headless { get; init; }

    /// <summary><c>real</c> or <c>fixed</c>; the app's default if null (fixed when headless).</summary>
    public string? Clock { get; init; }

    /// <summary>The window's (or headless frame's) size in logical points, e.g. <c>1200x800</c>.</summary>
    public string? Size { get; init; }

    /// <summary>Pixels per point when headless.</summary>
    public float? Scale { get; init; }

    /// <summary>Where the app writes its interaction log; its default if null.</summary>
    public string? LogPath { get; init; }

    /// <summary>Arguments for the app.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Build configuration for a project (default Debug).</summary>
    public string Configuration { get; init; } = "Debug";

    /// <summary>How long to wait for it to be ready, after any build.</summary>
    public TimeSpan ReadyTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Where its standard output and error go; a file beside the logs if null.</summary>
    public string? OutputPath { get; init; }
}

/// <summary>An app <see cref="AgentLauncher"/> started.</summary>
/// <param name="Instance">Its registration, once ready.</param>
/// <param name="Process">Its process.</param>
/// <param name="OutputPath">Where its output goes.</param>
public sealed record LaunchedApp(InstanceInfo Instance, Process Process, string? OutputPath);

/// <summary>
/// Starts a Radiant UI app with automation on (<c>RADIANT_AGENT=1</c> and friends, which
/// <c>RadiantAutomation.Configure</c> reads) and waits until it has registered and is ready.
/// </summary>
public static class AgentLauncher
{
    /// <summary>Builds if need be, starts the app, and waits for it to be ready.</summary>
    public static async Task<LaunchedApp> LaunchAsync(AgentLaunchOptions options, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var (file, prefix) = await ResolveAsync(options, cancellation).ConfigureAwait(false);
        var name = options.Name ?? $"{Path.GetFileNameWithoutExtension(file).ToLowerInvariant()}-{Guid.NewGuid().ToString("N")[..6]}";
        var output = options.OutputPath ?? Path.Combine(InstanceRegistry.LogsDir, name + ".out");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);

        var start = new ProcessStartInfo { UseShellExecute = false };
        if (OperatingSystem.IsWindows())
        {
            start.FileName = prefix ?? file;
            if (prefix is not null)
            {
                start.ArgumentList.Add(file);
            }
            foreach (var argument in options.Arguments)
            {
                start.ArgumentList.Add(argument);
            }
        }
        else
        {
            // Through the shell, so output goes to a file the app keeps writing after whoever launched it exits.
            var command = string.Join(' ', new[] { prefix, file }.Where(p => p is not null).Concat(options.Arguments).Select(ShellQuote));
            start.FileName = "/bin/sh";
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add($"exec {command} >> {ShellQuote(output)} 2>&1");
        }
        start.Environment["RADIANT_AGENT"] = "1";
        start.Environment["RADIANT_AGENT_NAME"] = name;
        start.Environment["RADIANT_INSTANCES_DIR"] = InstanceRegistry.RootDir;
        if (options.Headless)
        {
            start.Environment["RADIANT_AGENT_HEADLESS"] = "1";
        }
        Set("RADIANT_AGENT_CLOCK", options.Clock);
        Set("RADIANT_AGENT_SIZE", options.Size);
        Set("RADIANT_AGENT_SCALE", options.Scale?.ToString(CultureInfo.InvariantCulture));
        Set("RADIANT_AGENT_LOG", options.LogPath);

        var process = Process.Start(start) ?? throw new InvalidOperationException($"Couldn't start {file}.");
        var deadline = DateTime.UtcNow + options.ReadyTimeout;
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            if (InstanceRegistry.GetInstance(name) is { Ready: true } info)
            {
                return new LaunchedApp(info, process, output);
            }
            if (process.HasExited)
            {
                throw new InvalidOperationException($"{Path.GetFileName(file)} exited ({process.ExitCode}) before it was ready. Its output:\n{Tail(output)}");
            }
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"{Path.GetFileName(file)} wasn't ready within {options.ReadyTimeout.TotalSeconds:0}s. Does it call RadiantAutomation.Configure? Its output:\n{Tail(output)}");
            }
            await Task.Delay(50, cancellation).ConfigureAwait(false);
        }

        void Set(string variable, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                start.Environment[variable] = value;
            }
        }
    }

    // The file to run, and what runs it (dotnet, for a dll).
    private static async Task<(string File, string? Prefix)> ResolveAsync(AgentLaunchOptions options, CancellationToken cancellation)
    {
        var target = Path.GetFullPath(options.Target);
        if (Directory.Exists(target))
        {
            var projects = Directory.GetFiles(target, "*.csproj");
            target = projects.Length == 1
                ? projects[0]
                : throw new InvalidOperationException($"{target} holds {projects.Length} projects; name the .csproj.");
        }
        if (target.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            // No build servers or reused nodes: they'd outlive the build holding its output open, and it would never end.
            await RunAsync("dotnet", ["build", target, "-c", options.Configuration, "-nologo", "-v", "q", "--disable-build-servers"], cancellation).ConfigureAwait(false);
            target = (await RunAsync("dotnet", ["msbuild", target, "-getProperty:TargetPath", "-p:Configuration=" + options.Configuration, "-nodeReuse:false"], cancellation).ConfigureAwait(false)).Trim();
        }
        if (!File.Exists(target))
        {
            throw new FileNotFoundException($"Nothing to launch at {target}.", target);
        }
        if (target.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var apphost = OperatingSystem.IsWindows() ? Path.ChangeExtension(target, ".exe") : target[..^4];
            return File.Exists(apphost) ? (apphost, null) : (target, "dotnet");
        }
        return (target, null);
    }

    private static async Task<string> RunAsync(string file, string[] arguments, CancellationToken cancellation)
    {
        var start = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Couldn't run {file}.");
        var output = process.StandardOutput.ReadToEndAsync(cancellation);
        var error = process.StandardError.ReadToEndAsync(cancellation);
        await process.WaitForExitAsync(cancellation).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{file} {string.Join(' ', arguments)} failed ({process.ExitCode}):\n{await output.ConfigureAwait(false)}{await error.ConfigureAwait(false)}");
        }
        return await output.ConfigureAwait(false);
    }

    private static string ShellQuote(string? text) => "'" + (text ?? "").Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static string Tail(string path)
    {
        try
        {
            var lines = File.ReadAllLines(path);
            return string.Join('\n', lines.Skip(Math.Max(0, lines.Length - 20)));
        }
        catch (IOException)
        {
            return "(none)";
        }
    }
}
