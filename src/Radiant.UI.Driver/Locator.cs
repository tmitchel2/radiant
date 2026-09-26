using System.Numerics;
using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Automation;

namespace Radiant.UI.Driver;

/// <summary>
/// The elements a selector means, found afresh each time it's used, so it survives rebuilds. Actions act
/// on the one it means (an <c>ambiguous</c> error if it means several: narrow it, or pick with
/// <see cref="Nth"/>); <see cref="Expect"/> waits for something to be true of it.
/// </summary>
public class Locator
{
    /// <summary>A locator for what <paramref name="selector"/> means in <paramref name="driver"/>'s app.</summary>
    protected internal Locator(AppDriver driver, Selector selector)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(selector);
        Driver = driver;
        Selector = selector;
    }

    /// <summary>The driver.</summary>
    public AppDriver Driver { get; }

    /// <summary>The selector.</summary>
    public Selector Selector { get; }

    /// <summary>Elements by their semantics role, typed: <c>scope.Role.Button("Save")</c>, within this.</summary>
    public RoleLocators Role => new(Driver, Selector);

    /// <summary>The <paramref name="index"/>th match, from 0; negative counts from the end.</summary>
    public Locator Nth(int index) => new(Driver, Selector.WithIndex(index));

    /// <summary>The first match.</summary>
    public Locator First() => Nth(0);

    /// <summary>The last match.</summary>
    public Locator Last() => Nth(-1);

    /// <summary>What <paramref name="selector"/> means inside this.</summary>
    public Locator Get(string selector) => new(Driver, Selector.Parse(selector) is var inner ? Nest(inner, Selector) : Selector);

    /// <summary>What <paramref name="selector"/> means inside this.</summary>
    public Locator Get(Selector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new(Driver, Nest(selector, Selector));
    }

    /// <summary>Those whose own label, value or text is <paramref name="text"/>.</summary>
    public Locator WithText(string text) => WithText(TextMatch.Exact(text));

    /// <summary>Those whose own label, value or text passes <paramref name="text"/>.</summary>
    public Locator WithText(TextMatch text) => new(Driver, Selector with { Text = text });

    /// <summary>Those labelled <paramref name="label"/>.</summary>
    public Locator WithLabel(string label) => WithLabel(TextMatch.Exact(label));

    /// <summary>Those whose label passes <paramref name="label"/>.</summary>
    public Locator WithLabel(TextMatch label) => new(Driver, Selector with { Label = label });

    /// <summary>Those with <paramref name="text"/> (a label, a value, some text) somewhere inside them.</summary>
    public Locator Containing(string text) => Containing(TextMatch.Exact(text));

    /// <summary>Those with something inside them whose label, value or text passes <paramref name="text"/>.</summary>
    public Locator Containing(TextMatch text) => new(Driver, Selector with { Has = new Selector { Text = text } });

    /// <summary>Those that are (or, false, aren't) selected: a tab, a row.</summary>
    public Locator Selected(bool selected = true) => new(Driver, Selector with { Selected = selected });

    /// <summary>Those that are (or aren't) checked.</summary>
    public Locator Checked(bool isChecked = true) => new(Driver, Selector with { Checked = isChecked });

    /// <summary>Those that are (or aren't) enabled.</summary>
    public Locator Enabled(bool enabled = true) => new(Driver, Selector with { Enabled = enabled });

    /// <summary>Those that can (or can't) be seen.</summary>
    public Locator Visible(bool visible = true) => new(Driver, Selector with { Visible = visible });

    /// <summary>Those that have (or haven't) keyboard focus.</summary>
    public Locator Focused(bool focused = true) => new(Driver, Selector with { Focused = focused });

    /// <summary>This, only inside <paramref name="scope"/>.</summary>
    public Locator Within(Locator scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return new Locator(Driver, Nest(Selector, scope.Selector));
    }

    /// <summary>Taps it; <paramref name="force"/> taps its centre even if it's covered or disabled.</summary>
    public Task<ActionResult> TapAsync(int count = 1, string? modifiers = null, bool force = false, CancellationToken cancellation = default) =>
        Act("ui.tap", new ElementActionParams { Selector = Selector, Count = count, Modifiers = modifiers, Force = force ? true : null }, cancellation);

    /// <summary>Double-taps it.</summary>
    public Task<ActionResult> DoubleTapAsync(CancellationToken cancellation = default) => TapAsync(2, cancellation: cancellation);

    /// <summary>Right-clicks it.</summary>
    public Task<ActionResult> RightClickAsync(CancellationToken cancellation = default) =>
        Act("ui.tap", new ElementActionParams { Selector = Selector, Button = "right" }, cancellation);

    /// <summary>Presses it as assistive technology does: focus and a click at its centre, whatever covers it.</summary>
    public Task<ActionResult> PressAsync(CancellationToken cancellation = default) =>
        Act("ui.press", new ElementActionParams { Selector = Selector }, cancellation);

    /// <summary>Holds the pointer down on it.</summary>
    public Task<ActionResult> LongPressAsync(TimeSpan? duration = null, CancellationToken cancellation = default) =>
        Act("ui.longPress", new ElementActionParams { Selector = Selector, DurationMs = (int?)duration?.TotalMilliseconds }, cancellation);

    /// <summary>Moves the pointer onto it.</summary>
    public Task<ActionResult> HoverAsync(CancellationToken cancellation = default) =>
        Act("ui.hover", new ElementActionParams { Selector = Selector }, cancellation);

    /// <summary>Gives it keyboard focus.</summary>
    public Task<ActionResult> FocusAsync(CancellationToken cancellation = default) =>
        Act("ui.focus", new ElementActionParams { Selector = Selector }, cancellation);

    /// <summary>Types into it (tapping it first if focus isn't in it).</summary>
    public Task<ActionResult> TypeAsync(string text, bool replace = false, bool submit = false, CancellationToken cancellation = default) =>
        Act("ui.type", new ElementActionParams { Selector = Selector, Text = text, Replace = replace, Submit = submit }, cancellation);

    /// <summary>Replaces what's in it with <paramref name="text"/>.</summary>
    public Task<ActionResult> FillAsync(string text, CancellationToken cancellation = default) => TypeAsync(text, replace: true, cancellation: cancellation);

    /// <summary>Scrolls whatever it's in until it's in view.</summary>
    public Task<ActionResult> ScrollIntoViewAsync(Locator? container = null, CancellationToken cancellation = default) =>
        Driver.CallAsync("ui.scrollTo", new ScrollToParams { Target = Selector, Container = container?.Selector },
            AutomationJsonContext.Default.ScrollToParams, AutomationJsonContext.Default.ActionResult, Driver.Timeout, cancellation);

    /// <summary>Scrolls it (a scroll area, or the one it's in) by <paramref name="by"/> pixels.</summary>
    public Task<ActionResult> ScrollAsync(Vector2 by, bool wheel = false, CancellationToken cancellation = default) =>
        Driver.CallAsync("ui.scroll", new ScrollParams { Selector = Selector, By = new PointValue(by.X, by.Y), Mode = wheel ? "wheel" : null },
            AutomationJsonContext.Default.ScrollParams, AutomationJsonContext.Default.ActionResult, Driver.Timeout, cancellation);

    /// <summary>Scrolls it to <c>top</c>, <c>bottom</c>, <c>start</c> or <c>end</c>.</summary>
    public Task<ActionResult> ScrollToAsync(string edge, CancellationToken cancellation = default) =>
        Driver.CallAsync("ui.scroll", new ScrollParams { Selector = Selector, To = edge },
            AutomationJsonContext.Default.ScrollParams, AutomationJsonContext.Default.ActionResult, Driver.Timeout, cancellation);

    /// <summary>Drags from it <paramref name="distance"/> pixels <c>up</c>, <c>down</c>, <c>left</c> or <c>right</c>.</summary>
    public Task<ActionResult> SwipeAsync(string direction, float distance = 300, CancellationToken cancellation = default) =>
        Driver.CallAsync("ui.swipe", new DragParams { Selector = Selector, Direction = direction, Distance = distance },
            AutomationJsonContext.Default.DragParams, AutomationJsonContext.Default.ActionResult, Driver.Timeout, cancellation);

    /// <summary>Drags it onto <paramref name="target"/>.</summary>
    public Task<ActionResult> DragToAsync(Locator target, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        return Driver.CallAsync("ui.drag", new DragParams { Selector = Selector, To = target.Selector },
            AutomationJsonContext.Default.DragParams, AutomationJsonContext.Default.ActionResult, Driver.Timeout, cancellation);
    }

    /// <summary>
    /// It, as JSON: the <paramref name="fields"/> asked for (see <see cref="InspectParams.Fields"/>) and
    /// <paramref name="depth"/> levels of children (-1 for all).
    /// </summary>
    public async Task<InspectNode> InspectAsync(string? fields = null, int depth = 0, CancellationToken cancellation = default)
    {
        var document = await Driver.CallAsync("ui.inspect", new InspectParams { Selector = Selector, Fields = fields, Depth = depth },
            AutomationJsonContext.Default.InspectParams, AutomationJsonContext.Default.InspectDocument, null, cancellation);
        return document.Nodes.Single();
    }

    /// <summary>Every match, briefly: where each is and where to tap it.</summary>
    public Task<QueryResult> QueryAsync(CancellationToken cancellation = default) =>
        Driver.CallAsync("ui.query", new QueryParams { Selector = Selector }, AutomationJsonContext.Default.QueryParams, AutomationJsonContext.Default.QueryResult, null, cancellation);

    /// <summary>How many elements match, now.</summary>
    public async Task<int> CountAsync(CancellationToken cancellation = default) => (await QueryAsync(cancellation)).Count;

    /// <summary>Whether any element matches, now.</summary>
    public async Task<bool> ExistsAsync(CancellationToken cancellation = default) => await CountAsync(cancellation) > 0;

    /// <summary>Its label (or value, or text), now.</summary>
    public async Task<string?> TextAsync(CancellationToken cancellation = default)
    {
        var node = await InspectAsync("label,value,text", 0, cancellation);
        return node.Label ?? node.Value ?? node.Text;
    }

    /// <summary>Waits until it's in <paramref name="state"/> (see <see cref="WaitForParams.State"/>).</summary>
    public Task<ActionResult> WaitForAsync(string state = "exists", TextMatch? text = null, TimeSpan? timeout = null, CancellationToken cancellation = default) =>
        Driver.CallAsync("ui.waitFor", new WaitForParams { Selector = Selector, State = state, Text = text },
            AutomationJsonContext.Default.WaitForParams, AutomationJsonContext.Default.ActionResult, timeout, cancellation);

    /// <summary>Expectations of it, each waiting until it holds.</summary>
    public Expectation Expect(TimeSpan? timeout = null) => new(this, timeout);

    /// <summary>The selector.</summary>
    public override string ToString() => Selector.ToString();

    private Task<ActionResult> Act(string action, ElementActionParams parameters, CancellationToken cancellation) =>
        Driver.CallAsync(action, parameters, AutomationJsonContext.Default.ElementActionParams, AutomationJsonContext.Default.ActionResult, null, cancellation);

    // Puts the outermost scope of inner inside outer.
    private static Selector Nest(Selector inner, Selector outer) =>
        inner.Within is { } within ? inner with { Within = Nest(within, outer) } : inner with { Within = outer };
}

/// <summary>What a test expects of an element; each waits until it holds, and fails with what was there if it doesn't.</summary>
public sealed class Expectation
{
    private readonly Locator _locator;
    private readonly TimeSpan? _timeout;

    internal Expectation(Locator locator, TimeSpan? timeout)
    {
        _locator = locator;
        _timeout = timeout;
    }

    /// <summary>That it exists.</summary>
    public Task<ActionResult> ToExistAsync() => Wait("exists");

    /// <summary>That some of it can be seen.</summary>
    public Task<ActionResult> ToBeVisibleAsync() => Wait("visible");

    /// <summary>That a tap would land on it.</summary>
    public Task<ActionResult> ToBeHittableAsync() => Wait("hittable");

    /// <summary>That nothing matches it.</summary>
    public Task<ActionResult> ToBeGoneAsync() => Wait("gone");

    /// <summary>That nothing matching it can be seen.</summary>
    public Task<ActionResult> ToBeHiddenAsync() => Wait("hidden");

    /// <summary>That it's enabled.</summary>
    public Task<ActionResult> ToBeEnabledAsync() => Wait("enabled");

    /// <summary>That it's disabled.</summary>
    public Task<ActionResult> ToBeDisabledAsync() => Wait("disabled");

    /// <summary>That it has focus.</summary>
    public Task<ActionResult> ToBeFocusedAsync() => Wait("focused");

    /// <summary>That it's checked.</summary>
    public Task<ActionResult> ToBeCheckedAsync() => Wait("checked");

    /// <summary>That it's selected.</summary>
    public Task<ActionResult> ToBeSelectedAsync() => Wait("selected");

    /// <summary>That it's not selected.</summary>
    public Task<ActionResult> ToBeUnselectedAsync() => Wait("unselected");

    /// <summary>That it's not checked.</summary>
    public Task<ActionResult> ToBeUncheckedAsync() => Wait("unchecked");

    /// <summary>That its label, value or text is <paramref name="text"/>.</summary>
    public Task<ActionResult> ToHaveTextAsync(string text) => Wait("exists", TextMatch.Exact(text));

    /// <summary>That its label, value or text passes <paramref name="text"/>.</summary>
    public Task<ActionResult> ToHaveTextAsync(TextMatch text) => Wait("exists", text);

    /// <summary>That its label, value or text contains <paramref name="text"/>, ignoring case.</summary>
    public Task<ActionResult> ToContainTextAsync(string text) => Wait("exists", TextMatch.Contains(text));

    /// <summary>That its value (a field's text) is <paramref name="value"/>.</summary>
    public Task<ActionResult> ToHaveValueAsync(string value) =>
        new Locator(_locator.Driver, _locator.Selector with { Value = TextMatch.Exact(value) }).WaitForAsync("exists", null, _timeout);

    private Task<ActionResult> Wait(string state, TextMatch? text = null) => _locator.WaitForAsync(state, text, _timeout);
}
