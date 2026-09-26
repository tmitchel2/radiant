using System.Globalization;
using System.Text.Json;
using Radiant.AgentCli;
using Radiant.Host.AgentControlProtocol;

return await Cli.RunAsync(args, Console.Out, Console.Error);

namespace Radiant.AgentCli
{
    /// <summary>
    /// <c>radiant-agent</c>: finds, inspects, acts on and records Radiant UI apps. Exit codes: 0 done;
    /// 1 the app said no (no match, ambiguous, covered…); 2 timed out; 3 no app to talk to; 64 bad usage.
    /// </summary>
    internal static class Cli
    {
        public const string Usage = """
            radiant-agent — drive Radiant UI apps (for agents, tests and people)

            Apps:
              ls                                       running instances
              launch <exe|dll|csproj|dir> [--headless] [--clock real|fixed] [--size WxH] [--scale n] [--name n] [--log path] [-- app args]
              info | actions | idle | exit
              step [--frames n | --ms n]               run frames; on the fixed clock, move time on

            Looking:
              tree [--all] [--depth n] [--fields f] [--render] [--root <sel>]
              find <sel>                               matches, with bounds and where to tap
              inspect <sel> [--fields f] [--depth n] [--all] [--render]      JSON
                fields: basic geometry visibility hit scroll semantics state text render screen all, or single
                fields (tap, bounds, visible, scroll, text…); -x leaves one out. Default basic,geometry,state.
              screenshot [out.png] [--annotate[=all]] [--crop <sel>] [--scale n]

            Acting (each waits for idle, scrolls into view, presses where it can be hit, waits for idle):
              tap <sel> [--count n] [--right] [--mods cmd+shift] [--force] [--no-scroll]  |  tap --at x,y [--px]
              press <sel> | long-press <sel> [--ms n] | hover <sel> | focus <sel>
              type [<sel>] <text> [--replace] [--submit]
              key <chord> [--repeat n]                 e.g. Tab, Enter, Cmd+S, Shift+Tab
              scroll [<sel>] (--by dx,dy | --to top|bottom|start|end) [--wheel]
              scroll-to <sel> [--in <sel>] [--dir down|up|left|right]
              swipe <sel> <up|down|left|right> [--distance n]
              drag <sel> (--to <sel> | --to-point x,y)
              wait <sel> [--visible|--hittable|--gone|--hidden|--enabled|--disabled|--focused|--checked|--unchecked] [--text t]
              wait idle

            The log:
              log [-f] [--json] [--src human,agent,test,app] [--since n] [--tail n] [--file path]
              log note <text>
              raw <action> ['<json params>']

            Selectors: @save (test ID), #412 (node id), role=button label="Save", text~=sav (contains),
              label=/^Sa/i, "Save" (text), checked, enabled=false, [2] (the third), @list >> text=Row (within).

            Options: -i/--instance <name> (or $RADIANT_AGENT_INSTANCE; the only UI instance if there's one),
              --transport auto|socket|file, --json, --timeout 10s, -q.
            """;

        public static async Task<int> RunAsync(string[] arguments, TextWriter stdout, TextWriter stderr)
        {
            try
            {
                var args = new Args(arguments);
                if (args.Positionals.Count == 0 || args.Has("help") || args.Positionals[0] == "help")
                {
                    stdout.WriteLine(Usage);
                    return args.Positionals.Count == 0 && !args.Has("help") ? 64 : 0;
                }
                return await new Session(args, stdout, stderr).RunAsync();
            }
            catch (UsageException e)
            {
                stderr.WriteLine($"usage: {e.Message}  (radiant-agent help)");
                return 64;
            }
            catch (InvalidOperationException e)
            {
                stderr.WriteLine($"error: {e.Message}");
                return 3;
            }
            catch (TimeoutException e)
            {
                stderr.WriteLine($"error timeout: {e.Message}");
                return 2;
            }
            catch (FileNotFoundException e)
            {
                stderr.WriteLine($"error: {e.Message}");
                return 3;
            }
        }

        private sealed class Session(Args args, TextWriter stdout, TextWriter stderr)
        {
            private readonly bool _json = args.Has("json");
            private readonly bool _quiet = args.Has("quiet");
            private IAgentClient? _client;

            private string Command => args.Positionals[0];

            private TimeSpan? Timeout => args.Duration("timeout");

            public async Task<int> RunAsync()
            {
                try
                {
                    return Command switch
                    {
                        "ls" => List(),
                        "launch" => await LaunchAsync(),
                        "info" => await ShowAsync("app.info", null),
                        "actions" => await ActionsAsync(),
                        "idle" => await ShowAsync("app.idle", new Params().Set("settleFrames", args.Int("settle")).Build()),
                        "exit" => await ActAsync("app.exit", new Params().Build()),
                        "step" => await ShowAsync("app.step", new Params().Set("frames", args.Int("frames")).Set("ms", args.Float("ms")).Build()),
                        "tree" => await TreeAsync(),
                        "find" => await FindAsync(),
                        "inspect" => await InspectAsync(),
                        "screenshot" => await ScreenshotAsync(),
                        "tap" => await TapAsync(),
                        "press" => await ActAsync("ui.press", Element().Build()),
                        "long-press" => await ActAsync("ui.longPress", Element().Set("durationMs", args.Int("ms")).Build()),
                        "hover" => await ActAsync("ui.hover", Element().Build()),
                        "focus" => await ActAsync("ui.focus", Element().Build()),
                        "type" => await TypeAsync(),
                        "key" => await ActAsync("ui.key", new Params().Set("chord", args.Positional(1, "a key chord, e.g. Tab or Cmd+S")).Set("repeat", args.Int("repeat")).Build()),
                        "scroll" => await ActAsync("ui.scroll", new Params()
                            .Set("selector", args.Positionals.ElementAtOrDefault(1))
                            .Point("by", args.Pair("by"))
                            .Set("to", args.Get("to"))
                            .Set("mode", args.Has("wheel") ? "wheel" : null).Build()),
                        "scroll-to" => await ActAsync("ui.scrollTo", new Params()
                            .Set("target", args.Positional(1, "the element to bring into view"))
                            .Set("container", args.Get("in"))
                            .Set("direction", args.Get("dir")).Build()),
                        "swipe" => await ActAsync("ui.swipe", new Params()
                            .Set("selector", args.Positional(1, "the element to swipe on"))
                            .Set("direction", args.Positional(2, "a direction: up, down, left or right"))
                            .Set("distance", args.Float("distance")).Build()),
                        "drag" => await ActAsync("ui.drag", new Params()
                            .Set("selector", args.Positional(1, "the element to drag"))
                            .Set("to", args.Get("to"))
                            .Point("toPoint", args.Pair("to-point")).Build()),
                        "wait" => await WaitAsync(),
                        "log" => await LogAsync(),
                        "raw" => await RawAsync(),
                        _ => throw new UsageException($"Unknown command '{Command}'."),
                    };
                }
                finally
                {
                    if (_client is not null)
                    {
                        await _client.DisposeAsync();
                    }
                }
            }

            // ------------------------------------------------------------ apps

            private int List()
            {
                var instances = InstanceRegistry.ListInstances();
                if (_json)
                {
                    stdout.WriteLine(Output.Json(JsonSerializer.SerializeToElement(instances, AgentJsonContext.Default.InstanceInfoArray)));
                }
                else if (instances.Length == 0)
                {
                    stdout.WriteLine($"No instances running (under {InstanceRegistry.RootDir}).");
                }
                else
                {
                    stdout.WriteLine(Output.Instances(instances));
                }
                return 0;
            }

            private async Task<int> LaunchAsync()
            {
                var launched = await AgentLauncher.LaunchAsync(new AgentLaunchOptions
                {
                    Target = args.Positional(1, "what to launch: an executable, a dll, a .csproj or its directory"),
                    Name = args.Get("name"),
                    Headless = args.Has("headless"),
                    Clock = args.Get("clock"),
                    Size = args.Get("size"),
                    Scale = args.Float("scale"),
                    LogPath = args.Get("log"),
                    Arguments = args.Rest,
                    ReadyTimeout = Timeout ?? TimeSpan.FromSeconds(60),
                });
                var info = launched.Instance;
                if (_json)
                {
                    stdout.WriteLine(Output.Json(JsonSerializer.SerializeToElement(info, AgentJsonContext.Default.InstanceInfo)));
                }
                else
                {
                    stdout.WriteLine(info.Name);
                    if (!_quiet)
                    {
                        stderr.WriteLine($"pid {info.Pid}, {(info.Headless ? "headless" : "window")}, {info.Clock} clock; log {info.LogPath}; output {launched.OutputPath}");
                    }
                }
                return 0;
            }

            private async Task<int> ActionsAsync()
            {
                var response = await SendAsync("actions.list", null);
                if (Failed(response, out var code))
                {
                    return code;
                }
                if (_json)
                {
                    stdout.WriteLine(Output.Json(response.Result!.Value));
                    return 0;
                }
                foreach (var action in response.Result!.Value.EnumerateArray())
                {
                    stdout.WriteLine($"{Output.String(action, "name"),-16} {Output.String(action, "description")}");
                }
                return 0;
            }

            // ------------------------------------------------------------ looking

            private async Task<int> TreeAsync()
            {
                var response = await SendAsync("ui.tree", new Params()
                    .Set("fields", args.Get("fields") ?? (_json ? null : "basic,geometry,state,-kind"))
                    .Set("depth", args.Int("depth"))
                    .Set("visibleOnly", !args.Has("all"))
                    .Set("tree", args.Has("render") ? "render" : null)
                    .Set("root", args.Get("root")).Build());
                if (Failed(response, out var code))
                {
                    return code;
                }
                if (_json)
                {
                    stdout.WriteLine(Output.Json(response.Result!.Value));
                }
                else
                {
                    Output.Tree(stdout, response.Result!.Value);
                }
                return 0;
            }

            private async Task<int> FindAsync()
            {
                var response = await SendAsync("ui.query", new Params().Set("selector", args.Positional(1, "a selector")).Build());
                if (Failed(response, out var code))
                {
                    return code;
                }
                var result = response.Result!.Value;
                if (_json)
                {
                    stdout.WriteLine(Output.Json(result));
                    return 0;
                }
                foreach (var match in result.GetProperty("matches").EnumerateArray())
                {
                    stdout.WriteLine(Output.Node(match));
                }
                if (!_quiet)
                {
                    stderr.WriteLine($"{result.GetProperty("count").GetInt32()} match(es)");
                }
                return result.GetProperty("count").GetInt32() == 0 ? 1 : 0;
            }

            private async Task<int> InspectAsync()
            {
                var response = await SendAsync("ui.inspect", new Params()
                    .Set("selector", args.Positional(1, "a selector"))
                    .Set("fields", args.Get("fields"))
                    .Set("depth", args.Int("depth"))
                    .Set("all", args.Has("all") ? true : null)
                    .Set("tree", args.Has("render") ? "render" : null).Build());
                if (Failed(response, out var code))
                {
                    return code;
                }
                // The nodes are what was asked for; the document around them is in --json.
                stdout.WriteLine(Output.Json(_json ? response.Result!.Value : Nodes(response.Result!.Value)));
                return 0;

                static JsonElement Nodes(JsonElement document)
                {
                    var nodes = document.GetProperty("nodes");
                    return nodes.GetArrayLength() == 1 ? nodes[0] : nodes;
                }
            }

            private async Task<int> ScreenshotAsync()
            {
                var annotate = args.Has("annotate") ? args.Get("annotate") ?? "interactive" : null;
                var path = args.Positionals.ElementAtOrDefault(1);
                var response = await SendAsync("ui.screenshot", new Params()
                    .Set("path", path is null ? null : Path.GetFullPath(path))
                    .Set("annotate", annotate)
                    .Set("selector", args.Get("crop"))
                    .Set("scale", args.Float("scale")).Build());
                if (Failed(response, out var code))
                {
                    return code;
                }
                var result = response.Result!.Value;
                if (_json)
                {
                    stdout.WriteLine(Output.Json(result));
                    return 0;
                }
                stdout.WriteLine(Output.String(result, "path"));
                if (!_quiet)
                {
                    stderr.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"{result.GetProperty("width").GetInt32()}×{result.GetProperty("height").GetInt32()} px, {result.GetProperty("pixelScale").GetSingle()} px per point (divide pixel positions by it for tap --at)"));
                }
                if (result.TryGetProperty("marks", out var marks))
                {
                    foreach (var mark in marks.EnumerateArray())
                    {
                        stdout.WriteLine($"[{mark.GetProperty("mark").GetInt32()}] {Output.Node(mark)}");
                    }
                }
                return 0;
            }

            // ------------------------------------------------------------ acting

            private Params Element() => new Params()
                .Set("selector", args.Positional(1, "a selector, e.g. @save or role=button label=Save"))
                .Set("modifiers", args.Get("mods"))
                .Set("force", args.Has("force") ? true : null)
                .Set("scroll", args.Has("no-scroll") ? "none" : null);

            private Task<int> TapAsync()
            {
                if (args.Pair("at") is { } at)
                {
                    return ActAsync("ui.tapAt", new Params()
                        .Set("x", at.X).Set("y", at.Y)
                        .Set("space", args.Has("px") ? "pixels" : null)
                        .Set("count", args.Int("count"))
                        .Set("button", args.Has("right") ? "right" : null)
                        .Set("modifiers", args.Get("mods")).Build());
                }
                return ActAsync("ui.tap", Element()
                    .Set("count", args.Int("count"))
                    .Set("button", args.Has("right") ? "right" : null).Build());
            }

            private Task<int> TypeAsync()
            {
                var (selector, text) = args.Positionals.Count switch
                {
                    2 => (null, args.Positionals[1]),
                    3 => (args.Positionals[1], args.Positionals[2]),
                    _ => throw new UsageException("type takes [<selector>] <text>."),
                };
                return ActAsync("ui.type", new Params()
                    .Set("selector", selector)
                    .Set("text", text)
                    .Set("replace", args.Has("replace") ? true : null)
                    .Set("submit", args.Has("submit") ? true : null).Build());
            }

            private Task<int> WaitAsync()
            {
                var what = args.Positional(1, "a selector, or idle");
                if (what == "idle")
                {
                    return ShowAsync("app.idle", new Params().Build());
                }
                var states = new[] { "visible", "hittable", "gone", "hidden", "enabled", "disabled", "focused", "checked", "unchecked", "exists" };
                var state = args.Get("state") ?? states.FirstOrDefault(args.Has) ?? "exists";
                return ActAsync("ui.waitFor", new Params().Set("selector", what).Set("state", state).Set("text", args.Get("text")).Build());
            }

            private async Task<int> ActAsync(string action, JsonElement parameters)
            {
                var response = await SendAsync(action, parameters);
                if (Failed(response, out var code))
                {
                    return code;
                }
                if (_json)
                {
                    stdout.WriteLine(Output.Json(response.Result ?? default));
                }
                else if (!_quiet)
                {
                    stdout.WriteLine(Output.Action(action, response.Result ?? default));
                }
                return 0;
            }

            private async Task<int> ShowAsync(string action, JsonElement? parameters)
            {
                var response = await SendAsync(action, parameters);
                if (Failed(response, out var code))
                {
                    return code;
                }
                stdout.WriteLine(Output.Json(response.Result ?? default));
                return 0;
            }

            private async Task<int> RawAsync()
            {
                var action = args.Positional(1, "an action, e.g. ui.tree (radiant-agent actions lists them)");
                JsonElement? parameters = null;
                if (args.Positionals.ElementAtOrDefault(2) is { } json)
                {
                    try
                    {
                        using var document = JsonDocument.Parse(json);
                        parameters = document.RootElement.Clone();
                    }
                    catch (JsonException e)
                    {
                        throw new UsageException($"The params aren't JSON: {e.Message}");
                    }
                }
                var response = await SendAsync(action, parameters);
                stdout.WriteLine(Output.Json(JsonSerializer.SerializeToElement(response, AgentJsonContext.Default.AgentResponse)));
                return Failed(response, out var code, quiet: true) ? code : 0;
            }

            // ------------------------------------------------------------ the log

            private async Task<int> LogAsync()
            {
                if (args.Positionals.ElementAtOrDefault(1) == "note")
                {
                    return await ActAsync("log.note", new Params().Set("text", string.Join(' ', args.Positionals.Skip(2))).Build());
                }
                var sources = args.Get("src") is { } src ? src.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet() : null;
                var file = args.Get("file");
                if (file is null && !args.Has("follow"))
                {
                    // What's been written so far: the instance's log file.
                    var info = InstanceRegistry.GetInstance(AgentClient.ResolveInstance(args.Get("instance")))
                        ?? throw new InvalidOperationException("That instance isn't running.");
                    file = info.LogPath ?? throw new InvalidOperationException("That instance writes no log.");
                }
                if (file is not null)
                {
                    return await TailFileAsync(file, sources);
                }
                var client = await ClientAsync();
                if (client.Events is not { } events)
                {
                    // Files can't stream: follow the log file instead.
                    var info = InstanceRegistry.GetInstance(client.Instance);
                    return await TailFileAsync(info?.LogPath ?? throw new InvalidOperationException("That instance writes no log."), sources);
                }
                var response = await client.SendAsync("log.subscribe", new Params().Set("since", args.Int("since")).Set("src", args.Get("src")).Build());
                if (Failed(response, out var code))
                {
                    return code;
                }
                using var stop = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    stop.Cancel();
                };
                try
                {
                    await foreach (var agentEvent in events.ReadAllAsync(stop.Token))
                    {
                        if (agentEvent.Data?.Deserialize(AgentJsonContext.Default.LogEntry) is { } entry)
                        {
                            Print(entry);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                return 0;
            }

            private async Task<int> TailFileAsync(string path, HashSet<string>? sources)
            {
                var since = args.Int("since") ?? -1;
                var tail = args.Int("tail");
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                var lines = new List<LogEntry>();
                while (await reader.ReadLineAsync() is { } line)
                {
                    if (LogFormatter.FromJsonLine(line) is { } entry && entry.Seq > since && (sources is null || sources.Contains(entry.Src)))
                    {
                        lines.Add(entry);
                    }
                }
                foreach (var entry in tail is { } count ? lines.Skip(Math.Max(0, lines.Count - count)) : lines)
                {
                    Print(entry);
                }
                if (!args.Has("follow"))
                {
                    return 0;
                }
                using var stop = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    stop.Cancel();
                };
                try
                {
                    while (!stop.IsCancellationRequested)
                    {
                        if (await reader.ReadLineAsync(stop.Token) is { } line)
                        {
                            if (LogFormatter.FromJsonLine(line) is { } entry && (sources is null || sources.Contains(entry.Src)))
                            {
                                Print(entry);
                            }
                        }
                        else
                        {
                            await Task.Delay(100, stop.Token);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                return 0;
            }

            private void Print(LogEntry entry)
            {
                stdout.WriteLine(_json ? LogFormatter.ToJsonLine(entry) : LogFormatter.Format(entry));
                stdout.Flush();
            }

            // ------------------------------------------------------------ talking

            private async Task<IAgentClient> ClientAsync()
            {
                if (_client is not null)
                {
                    return _client;
                }
                var transport = args.Get("transport")?.ToLowerInvariant() switch
                {
                    null or "auto" => AgentTransport.Auto,
                    "socket" => AgentTransport.Socket,
                    "file" => AgentTransport.File,
                    var other => throw new UsageException($"Unknown transport '{other}': auto, socket or file."),
                };
                return _client = await AgentClient.ConnectAsync(AgentClient.ResolveInstance(args.Get("instance")), transport);
            }

            private async Task<AgentResponse> SendAsync(string action, JsonElement? parameters) =>
                await (await ClientAsync()).SendAsync(action, parameters, Timeout is { } timeout ? (int)timeout.TotalMilliseconds : null);

            // Reports an error response on stderr, with the exit code it means.
            private bool Failed(AgentResponse response, out int code, bool quiet = false)
            {
                if (response.Status != "error")
                {
                    code = 0;
                    return false;
                }
                var error = response.Error ?? new AgentError { Code = AgentErrorCodes.Internal, Message = "failed" };
                code = error.Code switch
                {
                    AgentErrorCodes.Timeout or AgentErrorCodes.Busy => 2,
                    AgentErrorCodes.Unreachable => 3,
                    AgentErrorCodes.InvalidParams or AgentErrorCodes.NotFound => 64,
                    _ => 1,
                };
                if (!quiet)
                {
                    stderr.WriteLine($"error {error.Code}: {error.Message}");
                    if (error.Details is { ValueKind: JsonValueKind.Array } details)
                    {
                        foreach (var node in details.EnumerateArray())
                        {
                            stderr.WriteLine("  " + Output.Node(node));
                        }
                    }
                }
                return true;
            }
        }
    }
}
