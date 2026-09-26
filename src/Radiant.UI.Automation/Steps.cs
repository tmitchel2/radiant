using System.Numerics;
using System.Text.Json;
using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Core;

namespace Radiant.UI.Automation;

/// <summary>A value a step sequence hands on, since iterators can't return one.</summary>
internal sealed class Box<T>
{
    public T? Value { get; set; }
}

/// <summary>
/// An action written as a sequence of frames: it yields null to wait for the next frame and a response
/// to finish. It remembers what it's waiting for, so a timeout says why (still busy, no match, covered).
/// </summary>
internal sealed class StepOperation : AgentOperation
{
    private readonly IEnumerator<AgentResponse?> _steps;
    private readonly ActionScope _scope;

    public StepOperation(ActionScope scope, IEnumerable<AgentResponse?> steps)
    {
        _scope = scope;
        _steps = steps.GetEnumerator();
    }

    public override AgentResponse? Poll()
    {
        if (!_steps.MoveNext())
        {
            _steps.Dispose();
            return AgentResponse.Ok("", null, 0);
        }
        var response = _steps.Current;
        if (response is not null)
        {
            _steps.Dispose();
        }
        return response;
    }

    public override AgentResponse OnTimeout(AgentCallContext context) =>
        _scope.Waiting?.Invoke() ?? base.OnTimeout(context);

    // Disposing the iterator runs its finally blocks: a held pointer is let go.
    public override void OnCancel() => _steps.Dispose();
}

/// <summary>What an action has to work with, and the waits it's built from.</summary>
internal sealed class ActionScope(UIAutomation automation, AgentCallContext call)
{
    public UIAutomation Automation { get; } = automation;

    public AgentCallContext Call { get; } = call;

    public UIAppSession Session => Automation.Session;

    public UIRoot Root => Automation.Session.Root;

    /// <summary>The error to answer if the action times out now.</summary>
    public Func<AgentResponse>? Waiting { get; set; }

    /// <summary>Frames since it started.</summary>
    public long Frames => Session.Frame - Call.StartFrame;

    /// <summary>Waits until the app has been idle for <paramref name="settleFrames"/> frames running.</summary>
    public IEnumerable<AgentResponse?> Settle(int settleFrames = 2)
    {
        Waiting = () => AgentResponse.Err("", AgentErrorCodes.Busy, "The app didn't become idle: " + string.Join("; ", Root.BusyReasons()),
            Json.Details(("busy", Root.BusyReasons())));
        var calm = 0;
        while (true)
        {
            calm = Root.IsIdle ? calm + 1 : 0;
            if (calm >= settleFrames)
            {
                yield break;
            }
            yield return null;
        }
    }

    /// <summary>Waits a frame for what was just done to be built, then until the app settles.</summary>
    public IEnumerable<AgentResponse?> SettleAfter(bool wait = true)
    {
        yield return null;
        if (wait)
        {
            foreach (var step in Settle())
            {
                yield return step;
            }
        }
    }

    /// <summary>Waits until <paramref name="selector"/> matches one node, and hands it on.</summary>
    public IEnumerable<AgentResponse?> Resolve(Selector selector, Box<Match> found)
    {
        SelectorEngine? last = null;
        Waiting = () => NoMatch(selector, last);
        while (true)
        {
            last = new SelectorEngine(Root);
            if (last.FindOne(selector) is { } match)
            {
                found.Value = match;
                yield break;
            }
            yield return null;
        }
    }

    /// <summary>
    /// Brings the node into view if some of it is hidden and a scroll area can show it, then waits until
    /// something at a point on it would take a press (or, forced, just its centre), and hands that point on.
    /// </summary>
    public IEnumerable<AgentResponse?> Reach(Selector selector, Box<Match> found, Box<Vector2> point, bool scroll, bool force)
    {
        if (scroll && found.Value!.Node.VisibleRatio < 0.999f && Root.ScrollIntoView(found.Value.Id))
        {
            foreach (var step in Settle())
            {
                yield return step;
            }
            foreach (var step in Resolve(selector, found))
            {
                yield return step;
            }
        }
        if (force)
        {
            var bounds = found.Value!.Node.Bounds;
            point.Value = new Vector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            yield break;
        }
        UINode? cover = null;
        AgentResponse Covered()
        {
            var match = found.Value!;
            if (match.Node.VisibleBounds is null)
            {
                return AgentResponse.Err("", AgentErrorCodes.NotVisible, $"{SelectorEngine.Describe(match)} can't be seen at {Geometry.Rect(match.Node.Bounds)}: it's clipped, off the window or transparent.");
            }
            var engine = new SelectorEngine(Root);
            var covering = cover is null ? null : engine.NearestSemantic(cover);
            return AgentResponse.Err("", AgentErrorCodes.NotHittable,
                $"{SelectorEngine.Describe(match)} is covered{(covering is null ? "" : " by " + SelectorEngine.Describe(covering))}.",
                covering is null ? null : Json.Element(Refs.Of(covering, Root), AutomationJsonContext.Default.NodeRef));
        }
        while (true)
        {
            Waiting = Covered;
            var (tap, obscuredBy) = Geometry.TapPoint(Root, found.Value!.Node);
            if (tap is { } at)
            {
                point.Value = at;
                yield break;
            }
            cover = obscuredBy;
            yield return null;
            // It may have been rebuilt, or gone, since.
            if (new SelectorEngine(Root).FindOne(selector) is { } again)
            {
                found.Value = again;
                continue;
            }
            foreach (var step in Resolve(selector, found))
            {
                yield return step;
            }
        }
    }

    public AgentResponse Result(Match? target, Vector2? at = null, string? value = null, ScrollValue? scroll = null, bool withFocus = false) =>
        Json.Ok(new ActionResult
        {
            Target = target is null ? null : Refs.Of(target, Root),
            At = at is { } point ? Geometry.Point(point) : null,
            Frames = Frames,
            Idle = Root.IsIdle,
            Value = value,
            FocusedId = withFocus ? Root.FocusedId : null,
            Scroll = scroll,
        }, AutomationJsonContext.Default.ActionResult);

    public AgentResponse NoMatch(Selector selector, SelectorEngine? engine)
    {
        engine ??= new SelectorEngine(Root);
        var suggestions = engine.Suggest(selector);
        var hint = suggestions.Count == 0 ? "" : " Did you mean " + string.Join(", ", suggestions.Select(SelectorEngine.Describe)) + "?";
        return AgentResponse.Err("", AgentErrorCodes.NoMatch, $"Nothing matches {selector}.{hint}",
            Json.Refs(suggestions.Select(s => Refs.Of(s, Root))));
    }

    /// <summary>Runs <paramref name="input"/> as the agent's, so the log doesn't take it for a person's.</summary>
    public void Input(Action input) => Automation.Synthesize(input);

    /// <summary>A pointer click (or several) at <paramref name="at"/>, hovering there first as a mouse would.</summary>
    public void Click(Vector2 at, PointerButton button, KeyModifiers modifiers, int count)
    {
        Input(() =>
        {
            Root.PointerMove(at, modifiers);
            for (var i = 0; i < Math.Max(count, 1); i++)
            {
                Root.PointerDown(at, button, modifiers);
                Root.PointerUp(at, button, modifiers);
            }
        });
    }

    public static ScrollValue? ScrollOf(UINode node) => node.Scroll is { } s
        ? new ScrollValue(Geometry.Point(s.Offset), Geometry.Point(s.MaxOffset), new SizeValue(s.ContentSize.X, s.ContentSize.Y), new SizeValue(s.ViewportSize.X, s.ViewportSize.Y), s.IsAnimating)
        : null;
}

/// <summary>Reading the small things params are made of.</summary>
internal static class ParamText
{
    public static PointerButton Button(string? name) => name?.ToLowerInvariant() switch
    {
        null or "" or "left" => PointerButton.Left,
        "right" => PointerButton.Right,
        "middle" => PointerButton.Middle,
        _ => throw new AgentException(AgentErrorCodes.InvalidParams, $"Unknown button '{name}': left, right or middle."),
    };

    public static KeyModifiers Modifiers(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return KeyModifiers.None;
        }
        // A chord with a stand-in key reads the modifiers the same way a chord's are read.
        return KeyChord.TryParse(text.Trim().TrimEnd('+') + "+A", out var chord)
            ? chord.Modifiers
            : throw new AgentException(AgentErrorCodes.InvalidParams, $"Unknown modifiers '{text}': e.g. shift, ctrl, alt, cmd, joined by +.");
    }

    public static KeyChord Chord(string? text) => KeyChord.TryParse(text, out var chord)
        ? chord
        : throw new AgentException(AgentErrorCodes.InvalidParams, $"Unknown key chord '{text}': e.g. \"Tab\", \"Cmd+S\", \"Shift+Enter\".");

    public static Selector Require(Selector? selector, string action, string name = "selector") =>
        selector ?? throw new AgentException(AgentErrorCodes.InvalidParams, $"{action} needs a {name}.");

    public static JsonElement? None => null;
}
