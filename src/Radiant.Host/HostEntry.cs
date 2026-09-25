namespace Radiant.Host;

/// <summary>
/// Entry point for the compositing "browser process" when the application's single binary is launched
/// with <c>--type host</c>. Owns the window + tab strip and blits each tab renderer's published frame. The
/// headless verification sub-modes (<c>--selftest</c> / <c>--compose-once</c> / <c>--input-test</c> /
/// <c>--control-test</c>) are dispatched here too. Receives the full process args (after <c>--type
/// host</c> is recognised by the app's launcher), so flags are matched by presence, not position.
/// </summary>
public static class HostEntry
{
    /// <summary>Runs the host as <paramref name="identity"/>, which it installs for this process.</summary>
    public static int Run(string[] args, RadiantAppIdentity identity)
    {
        RadiantAppIdentity.Use(identity);

        if (args.Contains("--selftest"))
        {
            var outPng = GetArg(args, "--out") ?? Path.Combine(Path.GetTempPath(), "radiant-host-selftest.png");
            return HostSelfTest.Run(outPng);
        }

        if (args.Contains("--compose-once"))
        {
            var instance = GetArg(args, "--instance");
            if (instance == null)
            {
                Console.Error.WriteLine("--compose-once requires --instance <name>");
                return 2;
            }
            var outPng = GetArg(args, "--out") ?? Path.Combine(Path.GetTempPath(), "radiant-host-compose.png");
            var timeout = int.TryParse(GetArg(args, "--timeout"), out var t) ? t : 5000;
            return ComposeOnce.Run(instance, outPng, timeout);
        }

        if (args.Contains("--input-test"))
        {
            var instance = GetArg(args, "--instance");
            if (instance == null)
            {
                Console.Error.WriteLine("--input-test requires --instance <name>");
                return 2;
            }
            var timeout = int.TryParse(GetArg(args, "--timeout"), out var it) ? it : 5000;
            return InputTest.Run(instance, timeout);
        }

        if (args.Contains("--control-test"))
        {
            // Headless check of the tab.* control actions against the live instance registry.
            return ControlTest.Run(GetArg(args, "--instance"));
        }

        // Default: launch the live compositing window. It discovers running renderer tabs, shows them
        // as tabs, and exposes tab.* control actions over the agent IPC under its instance name
        // (--name, default the identity's primary host name, "<prefix>-host").
        var hostName = GetArg(args, "--name") ?? identity.PrimaryHostName;

        // A given host name has a single owner. If a live host is already registered under this name
        // (e.g. a second `--type host` launched while the primary is up), refuse to start a competing
        // one: both would register the same instance dir and race on the shared command/response files,
        // and a lost File.Move race previously crashed the loser. ListInstances() prunes dead entries,
        // so a stale registration from a crashed host does not block a fresh start. Tear-off hosts use
        // unique names, so they are unaffected.
        if (AgentControlProtocol.InstanceRegistry.ListInstances()
                .Any(i => string.Equals(i.Name, hostName, StringComparison.Ordinal) && i.Capabilities.Contains("tab")))
        {
            Console.WriteLine($"{identity.Name}: a host named '{hostName}' is already running — not starting a second one.");
            return 0;
        }

        // `--follow`: a live-tear-off follower. Start hidden + focus-off and follow the cursor (read from
        // drag.json) until the drag ends, then settle as a normal window.
        var follow = args.Contains("--follow");
        using var host = new LiveHost(hostName, startHidden: follow);
        host.Run();
        return 0;
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
