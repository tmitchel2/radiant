using System.Numerics;
using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Core;

namespace Radiant.UI.Automation;

/// <summary>The <c>ui.*</c>, <c>app.*</c> and <c>log.*</c> actions.</summary>
internal static class UIActions
{
    public static void Register(UIAutomation automation)
    {
        var dispatcher = automation.Dispatcher;

        void Query(string name, string description, Func<ActionScope, AgentResponse> run) =>
            dispatcher.Register(Define(name, description), ActionKind.Query, call => AgentOperation.Done(run(new ActionScope(automation, call))));

        void Steps(string name, ActionKind kind, string description, Func<ActionScope, IEnumerable<AgentResponse?>> run) =>
            dispatcher.Register(Define(name, description), kind, call =>
            {
                var scope = new ActionScope(automation, call);
                return new StepOperation(scope, run(scope));
            });

        Query("app.info", "The app: its window, pixel scale, clock, frame, focus, and whether it's idle.", AppInfoOf);
        Steps("app.idle", ActionKind.Query, "Waits until the app is idle (nothing to rebuild, no animation or busy work) for settleFrames frames.", Idle);
        Steps("app.step", ActionKind.Mutation, "Runs frames: frames, or ms of UI time. On the fixed clock, moves time on (a snackbar's timeout, a tooltip's delay).", Step);
        Query("app.exit", "Ends the app after answering.", scope =>
        {
            scope.Session.Post(scope.Session.Exit);
            return Json.Ok(new ExitResult("exiting"), AutomationJsonContext.Default.ExitResult);
        });

        Query("ui.tree", "The tree, as JSON: tree (semantics|render), fields, depth, visibleOnly, root.", Tree);
        Query("ui.inspect", "A node (or every match, all) as JSON, with the fields asked for and depth levels of children.", Inspect);
        Query("ui.query", "The nodes a selector matches: id, role, label, testId, bounds, whether visible, and where to tap.", QueryNodes);
        Steps("ui.waitFor", ActionKind.Query, "Waits until an element exists, is visible, hittable, gone, hidden, enabled, disabled, focused, checked or unchecked, with text if given.", WaitFor);
        Steps("ui.screenshot", ActionKind.Query, "Writes a PNG of the UI (or of one element), optionally numbering the interactive nodes on it and listing them.", Screenshot);

        Steps("ui.tap", ActionKind.Mutation, "Taps an element: waits for idle, scrolls it into view, clicks where it can be hit, waits for idle.", Tap);
        Steps("ui.tapAt", ActionKind.Mutation, "Taps a point, logical or in screenshot pixels.", TapAt);
        Steps("ui.press", ActionKind.Mutation, "Presses an element as assistive technology does: focus and a click at its centre, whatever covers it.", Press);
        Steps("ui.longPress", ActionKind.Mutation, "Holds the pointer down on an element for durationMs.", LongPress);
        Steps("ui.hover", ActionKind.Mutation, "Moves the pointer onto an element.", Hover);
        Steps("ui.focus", ActionKind.Mutation, "Gives an element keyboard focus.", Focus);
        Steps("ui.type", ActionKind.Mutation, "Types text into an element (or the focused one): replace selects all first, submit presses Enter after.", TypeText);
        Steps("ui.key", ActionKind.Mutation, "Presses a key chord, e.g. \"Tab\", \"Cmd+S\", repeat times.", Key);
        Steps("ui.scroll", ActionKind.Mutation, "Scrolls a scroll area by a distance or to top, bottom, start or end.", Scroll);
        Steps("ui.scrollTo", ActionKind.Mutation, "Scrolls until an element is in view, stepping through a container while it isn't built yet.", ScrollTo);
        Steps("ui.swipe", ActionKind.Mutation, "Drags from an element (or a point) in a direction.", Drag);
        Steps("ui.drag", ActionKind.Mutation, "Drags from an element (or a point) to another element (or point).", Drag);

        Steps("log.subscribe", ActionKind.Query, "Streams the interaction log as log events (socket only): since, src, kinds.", Subscribe);
        Query("log.unsubscribe", "Stops a log subscription.", scope =>
        {
            var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.LogUnsubscribeParams);
            var stopped = parameters.Sub is { } sub && scope.Automation.Log.Unsubscribe(sub);
            return Json.Ok(new BoolResult(stopped), AgentJsonContext.Default.BoolResult);
        });
        Query("log.note", "Writes a note in the interaction log.", scope =>
        {
            var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.LogNoteParams);
            var entry = scope.Automation.Log.Note(parameters.Text ?? "", scope.Call.Connection.Client == "in-process" ? LogSources.Test : LogSources.Agent);
            return Json.Ok(new LogNoteResult(entry.Seq), AutomationJsonContext.Default.LogNoteResult);
        });
        Query("log.tail", "The last count (default 50) entries of the interaction log.", scope =>
        {
            var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.LogNoteParams);
            return Json.Ok(scope.Automation.Log.Tail(parameters.Count ?? 50).ToArray(), AutomationJsonContext.Default.LogEntryArray);
        });
    }

    private static ActionDefinition Define(string name, string description) => new()
    {
        Name = name,
        Category = name[..name.IndexOf('.', StringComparison.Ordinal)],
        Description = description,
        ParamsSchema = Schemas.For(name),
    };

    // ------------------------------------------------------------------ app

    private static AgentResponse AppInfoOf(ActionScope scope)
    {
        var session = scope.Session;
        return Json.Ok(new AppInfo
        {
            App = scope.Automation.AppName,
            Instance = scope.Automation.InstanceName,
            Pid = Environment.ProcessId,
            Window = new RectValue(session.WindowPosition.X, session.WindowPosition.Y, session.Size.X, session.Size.Y),
            PixelScale = session.PixelScale,
            Headless = session.IsHeadless,
            Clock = session.ClockMode.ToString().ToLowerInvariant(),
            Frame = session.Frame,
            Time = Math.Round(session.Time, 4),
            Idle = scope.Root.IsIdle,
            FocusedId = scope.Root.FocusedId,
            Log = scope.Automation.Log.Path,
        }, AutomationJsonContext.Default.AppInfo);
    }

    private static IEnumerable<AgentResponse?> Idle(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.IdleParams);
        foreach (var step in scope.Settle(Math.Max(1, parameters.SettleFrames ?? 2)))
        {
            yield return step;
        }
        yield return Json.Ok(new IdleResult
        {
            Idle = true,
            Frames = scope.Frames,
            Continuous = [.. scope.Root.RunningTickers(TickerKind.Continuous)],
            Timers = [.. scope.Root.RunningTickers(TickerKind.Timer)],
        }, AutomationJsonContext.Default.IdleResult);
    }

    private static IEnumerable<AgentResponse?> Step(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.StepParams);
        var until = parameters.Ms is { } ms ? scope.Session.Time + ms / 1000 : double.NaN;
        var frames = parameters.Frames ?? (parameters.Ms is null ? 1 : int.MaxValue);
        scope.Waiting = () => AgentResponse.Err("", AgentErrorCodes.Timeout, "Stepping took longer than the command's timeout; give it a longer one.");
        for (var i = 0; i < frames && !(scope.Session.Time >= until); i++)
        {
            yield return null;
        }
        yield return Json.Ok(new StepResult(scope.Session.Frame, Math.Round(scope.Session.Time, 4)), AutomationJsonContext.Default.StepResult);
    }

    // ------------------------------------------------------------------ inspecting

    private static AgentResponse Tree(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.TreeParams);
        var engine = new SelectorEngine(scope.Root);
        var roots = parameters.Root is { } root
            ? engine.Find(root) is { Count: > 0 } found ? found : throw Fail(scope.NoMatch(root, engine))
            : [.. engine.All.Take(1)];
        var inspector = new Inspector(scope.Session, engine, FieldSet.Parse(parameters.Fields), IsRender(parameters.Tree), parameters.VisibleOnly ?? false);
        return AgentResponse.Ok("", inspector.Document(roots, parameters.Depth ?? -1), 0);
    }

    private static AgentResponse Inspect(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.InspectParams);
        var selector = ParamText.Require(parameters.Selector, "ui.inspect");
        var engine = new SelectorEngine(scope.Root);
        var found = parameters.All == true ? engine.Find(selector) : engine.FindOne(selector) is { } one ? [one] : [];
        if (found.Count == 0)
        {
            throw Fail(scope.NoMatch(selector, engine));
        }
        var inspector = new Inspector(scope.Session, engine, FieldSet.Parse(parameters.Fields), IsRender(parameters.Tree), visibleOnly: false);
        return AgentResponse.Ok("", inspector.Document(found, parameters.Depth ?? 0), 0);
    }

    private static AgentResponse QueryNodes(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.QueryParams);
        var selector = ParamText.Require(parameters.Selector, "ui.query");
        var found = new SelectorEngine(scope.Root).Find(selector);
        return Json.Ok(new QueryResult(found.Count, [.. found.Take(100).Select(m => Refs.Of(m, scope.Root, withTap: true))]), AutomationJsonContext.Default.QueryResult);
    }

    private static bool IsRender(string? tree) => tree?.ToLowerInvariant() switch
    {
        null or "" or "semantics" => false,
        "render" => true,
        _ => throw new AgentException(AgentErrorCodes.InvalidParams, $"Unknown tree '{tree}': semantics or render."),
    };

    private static IEnumerable<AgentResponse?> WaitFor(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.WaitForParams);
        var selector = ParamText.Require(parameters.Selector, "ui.waitFor");
        var state = (parameters.State ?? "exists").ToLowerInvariant();
        Func<Match, bool> condition = state switch
        {
            "exists" or "gone" => _ => true,
            "visible" or "hidden" => m => m.Node.VisibleBounds is not null,
            "hittable" => m => Geometry.TapPoint(scope.Root, m.Node).Point is not null,
            "enabled" => m => m.Semantics?.Semantics.Disabled != true,
            "disabled" => m => m.Semantics?.Semantics.Disabled == true,
            "focused" => m => IsFocusedWithin(scope.Root, m.Node),
            "checked" => m => m.Semantics?.Semantics.Checked == true,
            "unchecked" => m => m.Semantics?.Semantics.Checked == false,
            "selected" => m => m.Semantics?.Semantics.Selected == true,
            "unselected" => m => m.Semantics?.Semantics.Selected != true,
            _ => throw new AgentException(AgentErrorCodes.InvalidParams, $"Unknown state '{state}': exists, visible, hittable, gone, hidden, enabled, disabled, focused, checked, unchecked, selected, unselected."),
        };
        var negative = state is "gone" or "hidden";
        var text = parameters.Text;
        List<Match> last = [];
        scope.Waiting = () => AgentResponse.Err("", AgentErrorCodes.Timeout,
            last.Count == 0
                ? $"{selector} never became {state}: nothing matches it{(negative ? "" : ".")}"
                : $"{selector} never became {state}: it matches {string.Join("; ", last.Take(3).Select(SelectorEngine.Describe))}{(text is null ? "" : $", text {text}")}.",
            Json.Refs(last.Take(10).Select(m => Refs.Of(m, scope.Root))));
        while (true)
        {
            last = [.. new SelectorEngine(scope.Root).Find(selector).Where(m => text is null || text.Matches(m.Label) || text.Matches(m.Value) || text.Matches(m.Node.Text))];
            var passing = last.FirstOrDefault(condition);
            if (negative ? passing is null : passing is not null)
            {
                yield return scope.Result(passing);
                yield break;
            }
            yield return null;
        }
    }

    private static IEnumerable<AgentResponse?> Screenshot(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.ScreenshotParams);
        var annotate = (parameters.Annotate ?? "none").ToLowerInvariant();
        if (annotate is not ("none" or "interactive" or "all"))
        {
            throw new AgentException(AgentErrorCodes.InvalidParams, $"Unknown annotate '{annotate}': none, interactive or all.");
        }
        if (parameters.WaitIdle ?? true)
        {
            foreach (var step in scope.Settle())
            {
                yield return step;
            }
        }
        var capture = scope.Automation.Capture()
            ?? throw new AgentException(AgentErrorCodes.Unsupported, "There's no GPU to draw a screenshot with here.");
        var engine = new SelectorEngine(scope.Root);
        System.Drawing.RectangleF? crop = null;
        if (parameters.Selector is { } selector)
        {
            var target = engine.FindOne(selector) ?? throw Fail(scope.NoMatch(selector, engine));
            crop = target.Node.VisibleBounds ?? target.Node.Bounds;
        }
        var marks = new List<ScreenshotMark>();
        if (annotate != "none")
        {
            foreach (var match in engine.All.Skip(1))
            {
                if (match.Node.VisibleBounds is not { } visible || crop is { } area && !area.IntersectsWith(visible) || !Marked(match, annotate == "all"))
                {
                    continue;
                }
                var tap = Geometry.TapPoint(scope.Root, match.Node).Point;
                marks.Add(new ScreenshotMark(marks.Count + 1, match.Id, match.Role, match.Label, match.TestId, Geometry.Rect(visible), tap is { } at ? Geometry.Point(at) : null));
            }
        }
        var pixelScale = scope.Session.PixelScale * Math.Clamp(parameters.Scale ?? 1f, 0.1f, 4f);
        var (rgba, width, height) = capture.Render(scope.Session, scope.Session.Size, pixelScale,
            [.. marks.Select(m => (m.Mark, new System.Drawing.RectangleF(m.Bounds.X, m.Bounds.Y, m.Bounds.W, m.Bounds.H)))]);
        var path = Path.GetFullPath(parameters.Path ?? scope.Automation.NextShotPath());
        System.Drawing.Rectangle? pixels = crop is { } c
            ? System.Drawing.Rectangle.Round(new System.Drawing.RectangleF(c.X * pixelScale, c.Y * pixelScale, c.Width * pixelScale, c.Height * pixelScale))
            : null;
        var (savedWidth, savedHeight) = FrameCapture.SavePng(path, rgba, width, height, pixels);
        yield return Json.Ok(new ScreenshotInfo
        {
            Path = path,
            Width = savedWidth,
            Height = savedHeight,
            PixelScale = pixelScale,
            Origin = crop is { } origin ? new PointValue(Geometry.Round(origin.X), Geometry.Round(origin.Y)) : new PointValue(0, 0),
            Marks = marks.Count == 0 ? null : [.. marks],
        }, AutomationJsonContext.Default.ScreenshotInfo);
    }

    // Which nodes an annotated screenshot numbers: what can be operated, or everything with a name.
    private static bool Marked(Match match, bool all)
    {
        if (all)
        {
            return match.Semantics?.Role is not (null or SemanticsRole.None or SemanticsRole.Group) || match.TestId is not null;
        }
        return match.Semantics is { } semantics && (semantics.IsFocusable || semantics.Role is SemanticsRole.Button or SemanticsRole.Link or SemanticsRole.CheckBox
            or SemanticsRole.RadioButton or SemanticsRole.Switch or SemanticsRole.Slider or SemanticsRole.TextField or SemanticsRole.Tab
            or SemanticsRole.MenuItem or SemanticsRole.ListItem or SemanticsRole.TreeItem or SemanticsRole.Row);
    }

    // ------------------------------------------------------------------ acting

    private static IEnumerable<AgentResponse?> Tap(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.ElementActionParams);
        var selector = ParamText.Require(parameters.Selector, "ui.tap");
        var button = ParamText.Button(parameters.Button);
        var modifiers = ParamText.Modifiers(parameters.Modifiers);
        var found = new Box<Match>();
        var point = new Box<Vector2>();
        foreach (var step in Approach(scope, parameters, selector, found, point))
        {
            yield return step;
        }
        var at = parameters.Offset is { } offset ? found.Value!.Node.ToRoot(new Vector2(offset.X, offset.Y)) : point.Value;
        scope.Click(at, button, modifiers, parameters.Count ?? 1);
        foreach (var step in scope.SettleAfter(parameters.WaitAfter ?? true))
        {
            yield return step;
        }
        yield return scope.Result(found.Value, at);
    }

    private static IEnumerable<AgentResponse?> TapAt(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.TapAtParams);
        var at = new Vector2(parameters.X, parameters.Y);
        if (parameters.Space?.ToLowerInvariant() is "pixels" or "px")
        {
            at /= scope.Session.PixelScale;
        }
        if (parameters.WaitIdle ?? true)
        {
            foreach (var step in scope.Settle())
            {
                yield return step;
            }
        }
        var hits = scope.Root.HitTest(at);
        var target = hits.Count > 0 ? new SelectorEngine(scope.Root).NearestSemantic(hits[0]) : null;
        scope.Click(at, ParamText.Button(parameters.Button), ParamText.Modifiers(parameters.Modifiers), parameters.Count ?? 1);
        foreach (var step in scope.SettleAfter(parameters.WaitAfter ?? true))
        {
            yield return step;
        }
        yield return scope.Result(target is { Id: not 0 } ? target : null, at);
    }

    private static IEnumerable<AgentResponse?> Press(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.ElementActionParams);
        var selector = ParamText.Require(parameters.Selector, "ui.press");
        var found = new Box<Match>();
        foreach (var step in WaitThenResolve(scope, parameters, selector, found))
        {
            yield return step;
        }
        var id = found.Value!.Id;
        scope.Input(() => scope.Root.Press(id));
        foreach (var step in scope.SettleAfter(parameters.WaitAfter ?? true))
        {
            yield return step;
        }
        yield return scope.Result(found.Value, withFocus: true);
    }

    private static IEnumerable<AgentResponse?> LongPress(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.ElementActionParams);
        var selector = ParamText.Require(parameters.Selector, "ui.longPress");
        var button = ParamText.Button(parameters.Button);
        var modifiers = ParamText.Modifiers(parameters.Modifiers);
        var found = new Box<Match>();
        var point = new Box<Vector2>();
        foreach (var step in Approach(scope, parameters, selector, found, point))
        {
            yield return step;
        }
        var at = point.Value;
        scope.Input(() =>
        {
            scope.Root.PointerMove(at, modifiers);
            scope.Root.PointerDown(at, button, modifiers);
        });
        var released = false;
        try
        {
            var until = scope.Session.Time + (parameters.DurationMs ?? 500) / 1000.0;
            scope.Waiting = () => AgentResponse.Err("", AgentErrorCodes.Timeout, "The press was held past the command's timeout; give it a longer one.");
            while (scope.Session.Time < until)
            {
                yield return null;
            }
            scope.Input(() => scope.Root.PointerUp(at, button, modifiers));
            released = true;
        }
        finally
        {
            if (!released)
            {
                scope.Input(() => scope.Root.PointerUp(at, button, modifiers));
            }
        }
        foreach (var step in scope.SettleAfter(parameters.WaitAfter ?? true))
        {
            yield return step;
        }
        yield return scope.Result(found.Value, at);
    }

    private static IEnumerable<AgentResponse?> Hover(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.ElementActionParams);
        var selector = ParamText.Require(parameters.Selector, "ui.hover");
        var found = new Box<Match>();
        var point = new Box<Vector2>();
        foreach (var step in Approach(scope, parameters, selector, found, point))
        {
            yield return step;
        }
        var at = point.Value;
        scope.Input(() => scope.Root.PointerMove(at, ParamText.Modifiers(parameters.Modifiers)));
        foreach (var step in scope.SettleAfter(parameters.WaitAfter ?? true))
        {
            yield return step;
        }
        yield return scope.Result(found.Value, at);
    }

    private static IEnumerable<AgentResponse?> Focus(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.ElementActionParams);
        var selector = ParamText.Require(parameters.Selector, "ui.focus");
        var found = new Box<Match>();
        foreach (var step in WaitThenResolve(scope, parameters, selector, found))
        {
            yield return step;
        }
        var target = new SelectorEngine(scope.Root).Focusable(found.Value!)
            ?? throw new AgentException(AgentErrorCodes.InvalidParams, $"{SelectorEngine.Describe(found.Value!)} can't take focus, and nothing in it can.");
        scope.Input(() => scope.Root.FocusNode(target.Id));
        foreach (var step in scope.SettleAfter(parameters.WaitAfter ?? true))
        {
            yield return step;
        }
        yield return scope.Result(found.Value, withFocus: true);
    }

    private static IEnumerable<AgentResponse?> TypeText(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.ElementActionParams);
        var text = parameters.Text ?? throw new AgentException(AgentErrorCodes.InvalidParams, "ui.type needs text.");
        Match? target = null;
        if (parameters.Selector is { } selector)
        {
            var found = new Box<Match>();
            var point = new Box<Vector2>();
            foreach (var step in Approach(scope, parameters, selector, found, point))
            {
                yield return step;
            }
            target = found.Value!;
            // Typing goes where focus is: a press puts it in the field, where the caret goes. A field
            // named on its outer box is pressed at its input.
            if (!IsFocusedWithin(scope.Root, target.Node))
            {
                var at = point.Value;
                if (new SelectorEngine(scope.Root).Focusable(target) is { } input && input.Id != target.Id && Geometry.TapPoint(scope.Root, input.Node).Point is { } inputPoint)
                {
                    at = inputPoint;
                }
                scope.Click(at, PointerButton.Left, KeyModifiers.None, 1);
                foreach (var step in scope.SettleAfter())
                {
                    yield return step;
                }
            }
        }
        else
        {
            if (scope.Root.FocusedId == 0)
            {
                throw new AgentException(AgentErrorCodes.InvalidParams, "ui.type with no selector types into the focused element, and nothing has focus.");
            }
            if (parameters.WaitIdle ?? true)
            {
                foreach (var step in scope.Settle())
                {
                    yield return step;
                }
            }
        }
        if (parameters.Replace == true)
        {
            var selectAll = KeyChord.Command(KeyCode.A);
            scope.Input(() =>
            {
                scope.Root.KeyDown(selectAll.Key, selectAll.Modifiers);
                scope.Root.KeyUp(selectAll.Key, selectAll.Modifiers);
            });
            yield return null;
        }
        scope.Input(() => scope.Root.TextInput(text));
        if (parameters.Submit == true)
        {
            yield return null;
            scope.Input(() =>
            {
                scope.Root.KeyDown(KeyCode.Enter);
                scope.Root.KeyUp(KeyCode.Enter);
            });
        }
        foreach (var step in scope.SettleAfter(parameters.WaitAfter ?? true))
        {
            yield return step;
        }
        var engine = new SelectorEngine(scope.Root);
        var after = parameters.Selector is { } again ? engine.Find(again).FirstOrDefault() : scope.Root.FindNode(scope.Root.FocusedId) is { } focused ? engine.NearestSemantic(focused) : null;
        yield return scope.Result(after ?? target, value: after?.Value, withFocus: true);
    }

    private static bool IsFocusedWithin(UIRoot root, UINode node) =>
        root.FindNode(root.FocusedId) is { } focused && (focused.Equals(node) || node.IsAncestorOf(focused));

    private static IEnumerable<AgentResponse?> Key(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.KeyParams);
        var chord = ParamText.Chord(parameters.Chord);
        if (parameters.WaitIdle ?? true)
        {
            foreach (var step in scope.Settle())
            {
                yield return step;
            }
        }
        for (var i = 0; i < Math.Max(1, parameters.Repeat ?? 1); i++)
        {
            scope.Input(() =>
            {
                scope.Root.KeyDown(chord.Key, chord.Modifiers);
                scope.Root.KeyUp(chord.Key, chord.Modifiers);
            });
            yield return null;
        }
        foreach (var step in scope.SettleAfter(parameters.WaitAfter ?? true))
        {
            yield return step;
        }
        var focused = scope.Root.FindNode(scope.Root.FocusedId) is { } node ? new SelectorEngine(scope.Root).NearestSemantic(node) : null;
        yield return scope.Result(focused, withFocus: true);
    }

    private static IEnumerable<AgentResponse?> Scroll(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.ScrollParams);
        foreach (var step in scope.Settle())
        {
            yield return step;
        }
        var area = FindScrollArea(scope, parameters.Selector);
        var info = area.Node.Scroll!;
        var target = info.Offset;
        if (parameters.By is { } by)
        {
            target += new Vector2(by.X, by.Y);
        }
        target = parameters.To?.ToLowerInvariant() switch
        {
            null => target,
            "top" => target with { Y = 0 },
            "bottom" => target with { Y = info.MaxOffset.Y },
            "start" or "left" => target with { X = 0 },
            "end" or "right" => target with { X = info.MaxOffset.X },
            var to => throw new AgentException(AgentErrorCodes.InvalidParams, $"Unknown scroll target '{to}': top, bottom, start or end."),
        };
        var wheel = (parameters.Mode ?? "direct").ToLowerInvariant() switch
        {
            "direct" => false,
            "wheel" => true,
            var mode => throw new AgentException(AgentErrorCodes.InvalidParams, $"Unknown mode '{mode}': direct or wheel."),
        };
        if (wheel)
        {
            var visible = area.Node.VisibleBounds ?? area.Node.Bounds;
            var centre = new Vector2(visible.X + visible.Width / 2, visible.Y + visible.Height / 2);
            var delta = target - info.Offset;
            scope.Input(() =>
            {
                scope.Root.PointerMove(centre);
                scope.Root.Wheel(centre, delta);
            });
        }
        else
        {
            scope.Root.ScrollTo(area.Id, target);
        }
        foreach (var step in scope.SettleAfter())
        {
            yield return step;
        }
        var after = scope.Root.FindNode(area.Id);
        yield return scope.Result(area, scroll: after is null ? null : ActionScope.ScrollOf(after));
    }

    private static Match FindScrollArea(ActionScope scope, Selector? selector)
    {
        var engine = new SelectorEngine(scope.Root);
        if (selector is null)
        {
            var areas = engine.All.Where(m => m.Node.Kind == UINodeKind.Scroll && m.Node.VisibleBounds is not null && m.Node.Scroll!.MaxOffset != Vector2.Zero).ToList();
            return areas.Count switch
            {
                1 => areas[0],
                0 => throw new AgentException(AgentErrorCodes.NoMatch, "There's no scroll area in view with anywhere to scroll."),
                _ => throw new AgentException(AgentErrorCodes.Ambiguous, $"{areas.Count} scroll areas are in view: {string.Join("; ", areas.Take(5).Select(SelectorEngine.Describe))}. Give a selector.",
                    Json.Refs(areas.Take(10).Select(a => Refs.Of(a, scope.Root)))),
            };
        }
        var match = engine.FindOne(selector) ?? throw Fail(scope.NoMatch(selector, engine));
        for (UINode? node = match.Node; node is not null; node = node.Parent)
        {
            if (node.Kind == UINodeKind.Scroll)
            {
                return engine.NearestSemantic(node) is { } area && area.Id == node.Id ? area : new Match(engine.SemanticsOf(node.Id), node, 0);
            }
        }
        throw new AgentException(AgentErrorCodes.InvalidParams, $"{SelectorEngine.Describe(match)} isn't in a scroll area.");
    }

    private static IEnumerable<AgentResponse?> ScrollTo(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.ScrollToParams);
        var target = ParamText.Require(parameters.Target, "ui.scrollTo", "target");
        var direction = Direction(parameters.Direction ?? "down");
        foreach (var step in scope.Settle())
        {
            yield return step;
        }
        SelectorEngine? engine = null;
        scope.Waiting = () => scope.NoMatch(target, engine);
        while (true)
        {
            engine = new SelectorEngine(scope.Root);
            if (engine.FindOne(target) is { } match)
            {
                scope.Root.ScrollIntoView(match.Id);
                foreach (var step in scope.SettleAfter())
                {
                    yield return step;
                }
                var found = new SelectorEngine(scope.Root).FindOne(target);
                yield return scope.Result(found ?? match);
                yield break;
            }
            if (parameters.Container is not { } container)
            {
                yield return null;
                continue;
            }
            // Not built yet: step the container on until it is, or there's nowhere further to go.
            var area = FindScrollArea(scope, container);
            var info = area.Node.Scroll!;
            var stride = parameters.Step ?? MathF.Max(20, (MathF.Abs(direction.X) > 0 ? info.ViewportSize.X : info.ViewportSize.Y) * 0.8f);
            var next = Vector2.Clamp(info.Offset + direction * stride, Vector2.Zero, Vector2.Max(info.MaxOffset, Vector2.Zero));
            if (next == info.Offset)
            {
                yield return scope.NoMatch(target, engine);
                yield break;
            }
            scope.Root.ScrollTo(area.Id, next);
            foreach (var step in scope.SettleAfter())
            {
                yield return step;
            }
        }
    }

    private static Vector2 Direction(string direction) => direction.ToLowerInvariant() switch
    {
        "down" => new Vector2(0, 1),
        "up" => new Vector2(0, -1),
        "right" => new Vector2(1, 0),
        "left" => new Vector2(-1, 0),
        _ => throw new AgentException(AgentErrorCodes.InvalidParams, $"Unknown direction '{direction}': up, down, left or right."),
    };

    private static IEnumerable<AgentResponse?> Drag(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.DragParams);
        foreach (var step in scope.Settle())
        {
            yield return step;
        }
        Match? origin = null;
        Vector2 from;
        if (parameters.Selector is { } selector)
        {
            var found = new Box<Match>();
            var point = new Box<Vector2>();
            foreach (var step in scope.Resolve(selector, found))
            {
                yield return step;
            }
            foreach (var step in scope.Reach(selector, found, point, scroll: true, force: false))
            {
                yield return step;
            }
            origin = found.Value;
            from = point.Value;
        }
        else if (parameters.From is { } start)
        {
            from = new Vector2(start.X, start.Y);
        }
        else
        {
            throw new AgentException(AgentErrorCodes.InvalidParams, $"{scope.Call.Command.Action} needs a selector or from.");
        }
        Vector2 to;
        if (parameters.To is { } toSelector)
        {
            var found = new Box<Match>();
            var point = new Box<Vector2>();
            foreach (var step in scope.Resolve(toSelector, found))
            {
                yield return step;
            }
            foreach (var step in scope.Reach(toSelector, found, point, scroll: false, force: true))
            {
                yield return step;
            }
            to = point.Value;
        }
        else if (parameters.ToPoint is { } end)
        {
            to = new Vector2(end.X, end.Y);
        }
        else if (parameters.Direction is { } direction)
        {
            to = from + Direction(direction) * (parameters.Distance ?? 300f);
        }
        else
        {
            throw new AgentException(AgentErrorCodes.InvalidParams, $"{scope.Call.Command.Action} needs to, toPoint or direction.");
        }
        var steps = Math.Clamp(parameters.Steps ?? 10, 1, 600);
        scope.Input(() =>
        {
            scope.Root.PointerMove(from);
            scope.Root.PointerDown(from);
        });
        var released = false;
        try
        {
            for (var i = 1; i <= steps; i++)
            {
                yield return null;
                var at = Vector2.Lerp(from, to, i / (float)steps);
                scope.Input(() => scope.Root.PointerMove(at));
            }
            scope.Input(() => scope.Root.PointerUp(to));
            released = true;
        }
        finally
        {
            if (!released)
            {
                scope.Input(() => scope.Root.PointerUp(to));
            }
        }
        foreach (var step in scope.SettleAfter())
        {
            yield return step;
        }
        yield return scope.Result(origin, to);
    }

    // Idle, then the element, in view and where it can be hit.
    private static IEnumerable<AgentResponse?> Approach(ActionScope scope, ElementActionParams parameters, Selector selector, Box<Match> found, Box<Vector2> point)
    {
        foreach (var step in WaitThenResolve(scope, parameters, selector, found))
        {
            yield return step;
        }
        var scroll = (parameters.Scroll ?? "auto").ToLowerInvariant() switch
        {
            "auto" => true,
            "none" => false,
            var other => throw new AgentException(AgentErrorCodes.InvalidParams, $"Unknown scroll '{other}': auto or none."),
        };
        foreach (var step in scope.Reach(selector, found, point, scroll, parameters.Force ?? false))
        {
            yield return step;
        }
    }

    private static IEnumerable<AgentResponse?> WaitThenResolve(ActionScope scope, ElementActionParams parameters, Selector selector, Box<Match> found)
    {
        if (parameters.WaitIdle ?? true)
        {
            foreach (var step in scope.Settle())
            {
                yield return step;
            }
        }
        foreach (var step in scope.Resolve(selector, found))
        {
            yield return step;
        }
    }

    private static IEnumerable<AgentResponse?> Subscribe(ActionScope scope)
    {
        var parameters = Json.Params(scope.Call, AutomationJsonContext.Default.LogSubscribeParams);
        if (!scope.Call.Connection.CanStream)
        {
            throw new AgentException(AgentErrorCodes.Unsupported,
                $"{scope.Call.Connection.Client} can't stream events; use the socket transport, or read the log file{(scope.Automation.Log.Path is { } path ? " at " + path : "")}.");
        }
        var id = scope.Automation.Log.Subscribe(scope.Call.Connection, parameters.Since ?? -1, Set(parameters.Src), Set(parameters.Kinds));
        yield return Json.Ok(new LogSubscription(id, scope.Automation.Log.Seq, scope.Automation.Log.Path), AutomationJsonContext.Default.LogSubscription);

        static HashSet<string>? Set(string? list) =>
            string.IsNullOrWhiteSpace(list) ? null : [.. list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    private static AgentException Fail(AgentResponse error) =>
        new(error.Error!.Code, error.Error.Message, error.Error.Details);
}
