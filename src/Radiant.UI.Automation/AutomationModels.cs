using System.Text.Json;
using System.Text.Json.Serialization;
using Radiant.Host.AgentControlProtocol;

namespace Radiant.UI.Automation;

// The params and results of the ui.*, app.* and log.* actions. Params are all optional unless an action
// says otherwise; coordinates are logical window points, top left at the origin, unless "pixels".

/// <summary>A point.</summary>
public sealed record PointValue(float X, float Y);

/// <summary>A rectangle: its top left, width and height.</summary>
public sealed record RectValue(float X, float Y, float W, float H);

/// <summary>A size.</summary>
public sealed record SizeValue(float W, float H);

/// <summary>A node, briefly: enough to tell which and where.</summary>
public sealed record NodeRef
{
    /// <summary>Its id.</summary>
    public int Id { get; init; }

    /// <summary>Its role.</summary>
    public string? Role { get; init; }

    /// <summary>Its label.</summary>
    public string? Label { get; init; }

    /// <summary>Its test ID.</summary>
    public string? TestId { get; init; }

    /// <summary>Where it is.</summary>
    public RectValue? Bounds { get; init; }

    /// <summary>Whether any of it can be seen.</summary>
    public bool? Visible { get; init; }

    /// <summary>Where to tap it, if it can be tapped.</summary>
    public PointValue? Tap { get; init; }
}

/// <summary>Params for <c>ui.tree</c>.</summary>
public sealed record TreeParams
{
    /// <summary><c>semantics</c> (the default): what assistive technology sees; <c>render</c>: every laid-out node.</summary>
    public string? Tree { get; init; }

    /// <summary>Field groups and fields, comma-separated; <c>-x</c> leaves one out. See <c>ui.inspect</c>.</summary>
    public string? Fields { get; init; }

    /// <summary>How many levels below the root to include; -1 (the default) for all.</summary>
    public int? Depth { get; init; }

    /// <summary>Leaves out what can't be seen.</summary>
    public bool? VisibleOnly { get; init; }

    /// <summary>Where to start: the whole tree if null.</summary>
    public Selector? Root { get; init; }
}

/// <summary>Params for <c>ui.inspect</c>.</summary>
public sealed record InspectParams
{
    /// <summary>The node (required).</summary>
    public Selector? Selector { get; init; }

    /// <summary>
    /// Field groups and fields, comma-separated, <c>-x</c> leaving one out. Groups: <c>basic</c> (id, role,
    /// label, testId, kind), <c>geometry</c> (bounds, size, position), <c>visibility</c> (visible,
    /// visibleRatio, opacity, onScreen), <c>hit</c> (tap, hittable, obscuredBy), <c>scroll</c>,
    /// <c>semantics</c> (value, description, headingLevel), <c>state</c> (disabled, checked, selected,
    /// expanded, focusable, focused), <c>text</c> (text, selection, placeholder), <c>render</c> (type,
    /// clips, hitTestVisible, transformed), <c>screen</c> (screenBounds, pixelBounds), <c>all</c>.
    /// Default <c>basic,geometry,state</c>. Fields from groups are left out when false or empty.
    /// </summary>
    public string? Fields { get; init; }

    /// <summary>How many levels of children to include: 0 (the default) for the node alone, -1 for all.</summary>
    public int? Depth { get; init; }

    /// <summary>Every match rather than one.</summary>
    public bool? All { get; init; }

    /// <summary>Which tree children come from: <c>semantics</c> or <c>render</c>.</summary>
    public string? Tree { get; init; }
}

/// <summary>Params for <c>ui.query</c>.</summary>
public sealed record QueryParams
{
    /// <summary>What to find (required).</summary>
    public Selector? Selector { get; init; }
}

/// <summary>What <c>ui.query</c> found.</summary>
public sealed record QueryResult(int Count, NodeRef[] Matches);

/// <summary>
/// Params for the actions on an element: <c>ui.tap</c>, <c>ui.press</c>, <c>ui.longPress</c>,
/// <c>ui.hover</c>, <c>ui.focus</c>, <c>ui.type</c>.
/// </summary>
public sealed record ElementActionParams
{
    /// <summary>The element; for <c>ui.type</c>, the focused one if null.</summary>
    public Selector? Selector { get; init; }

    /// <summary>Wait for the app to be idle first (default true).</summary>
    public bool? WaitIdle { get; init; }

    /// <summary><c>auto</c> (the default) scrolls it into view; <c>none</c> doesn't.</summary>
    public string? Scroll { get; init; }

    /// <summary>Wait for the app to be idle after (default true).</summary>
    public bool? WaitAfter { get; init; }

    /// <summary>Act even if it's covered: at its centre, whatever is there.</summary>
    public bool? Force { get; init; }

    /// <summary>For a tap: how many clicks (2 for a double click).</summary>
    public int? Count { get; init; }

    /// <summary>For a tap: <c>left</c> (the default), <c>right</c> or <c>middle</c>.</summary>
    public string? Button { get; init; }

    /// <summary>Modifiers held, e.g. <c>"shift"</c>, <c>"cmd+shift"</c>.</summary>
    public string? Modifiers { get; init; }

    /// <summary>For a tap: where in the element, from its top left, instead of a point that hits it.</summary>
    public PointValue? Offset { get; init; }

    /// <summary>For a long press: how long to hold, in milliseconds (default 500).</summary>
    public int? DurationMs { get; init; }

    /// <summary>For typing: the text.</summary>
    public string? Text { get; init; }

    /// <summary>For typing: select everything first, so the text replaces it.</summary>
    public bool? Replace { get; init; }

    /// <summary>For typing: press Enter after.</summary>
    public bool? Submit { get; init; }
}

/// <summary>Params for <c>ui.tapAt</c>.</summary>
public sealed record TapAtParams
{
    /// <summary>Where.</summary>
    public float X { get; init; }

    /// <summary>Where.</summary>
    public float Y { get; init; }

    /// <summary><c>logical</c> (the default) or <c>pixels</c>, as in a screenshot.</summary>
    public string? Space { get; init; }

    /// <summary>How many clicks.</summary>
    public int? Count { get; init; }

    /// <summary><c>left</c>, <c>right</c> or <c>middle</c>.</summary>
    public string? Button { get; init; }

    /// <summary>Modifiers held.</summary>
    public string? Modifiers { get; init; }

    /// <summary>Wait for idle first (default true).</summary>
    public bool? WaitIdle { get; init; }

    /// <summary>Wait for idle after (default true).</summary>
    public bool? WaitAfter { get; init; }
}

/// <summary>Params for <c>ui.key</c>.</summary>
public sealed record KeyParams
{
    /// <summary>The chord, e.g. <c>"Cmd+S"</c>, <c>"Tab"</c> (required).</summary>
    public string? Chord { get; init; }

    /// <summary>How many times.</summary>
    public int? Repeat { get; init; }

    /// <summary>Wait for idle first (default true).</summary>
    public bool? WaitIdle { get; init; }

    /// <summary>Wait for idle after (default true).</summary>
    public bool? WaitAfter { get; init; }
}

/// <summary>Params for <c>ui.scroll</c>.</summary>
public sealed record ScrollParams
{
    /// <summary>The scroll area, or something in it; the only one if null.</summary>
    public Selector? Selector { get; init; }

    /// <summary>How far, in pixels towards the content's end.</summary>
    public PointValue? By { get; init; }

    /// <summary>Where: <c>top</c>, <c>bottom</c>, <c>start</c>, <c>end</c>.</summary>
    public string? To { get; init; }

    /// <summary><c>direct</c> (the default) sets the offset; <c>wheel</c> sends wheel input.</summary>
    public string? Mode { get; init; }
}

/// <summary>Params for <c>ui.scrollTo</c>.</summary>
public sealed record ScrollToParams
{
    /// <summary>What to bring into view (required).</summary>
    public Selector? Target { get; init; }

    /// <summary>The scroll area to step through while it hasn't been built yet (a virtual list).</summary>
    public Selector? Container { get; init; }

    /// <summary><c>down</c> (the default), <c>up</c>, <c>right</c> or <c>left</c>.</summary>
    public string? Direction { get; init; }

    /// <summary>How far each step goes, in pixels; most of the viewport if null.</summary>
    public float? Step { get; init; }
}

/// <summary>Params for <c>ui.swipe</c> and <c>ui.drag</c>.</summary>
public sealed record DragParams
{
    /// <summary>Where to start: the element's tap point.</summary>
    public Selector? Selector { get; init; }

    /// <summary>Where to start, if not an element.</summary>
    public PointValue? From { get; init; }

    /// <summary>For a swipe: <c>up</c>, <c>down</c>, <c>left</c> or <c>right</c>.</summary>
    public string? Direction { get; init; }

    /// <summary>For a swipe: how far, in pixels (default 300).</summary>
    public float? Distance { get; init; }

    /// <summary>For a drag: the element to end on.</summary>
    public Selector? To { get; init; }

    /// <summary>For a drag: the point to end on.</summary>
    public PointValue? ToPoint { get; init; }

    /// <summary>How many moves between (default 10), one a frame.</summary>
    public int? Steps { get; init; }
}

/// <summary>Params for <c>ui.waitFor</c>.</summary>
public sealed record WaitForParams
{
    /// <summary>The element (required).</summary>
    public Selector? Selector { get; init; }

    /// <summary>
    /// <c>exists</c> (the default), <c>visible</c>, <c>hittable</c>, <c>gone</c>, <c>hidden</c>,
    /// <c>enabled</c>, <c>disabled</c>, <c>focused</c> (it or something in it has focus), <c>checked</c>, <c>unchecked</c>.
    /// </summary>
    public string? State { get; init; }

    /// <summary>What its label, value or text must match as well.</summary>
    public TextMatch? Text { get; init; }
}

/// <summary>Params for <c>app.idle</c>.</summary>
public sealed record IdleParams
{
    /// <summary>How many frames in a row it must be idle for (default 2).</summary>
    public int? SettleFrames { get; init; }
}

/// <summary>Params for <c>app.step</c>.</summary>
public sealed record StepParams
{
    /// <summary>How many frames.</summary>
    public int? Frames { get; init; }

    /// <summary>Or how many milliseconds of UI time.</summary>
    public double? Ms { get; init; }
}

/// <summary>Params for <c>ui.screenshot</c>.</summary>
public sealed record ScreenshotParams
{
    /// <summary>Where to write the PNG; beside the log (or in the temp directory) if null.</summary>
    public string? Path { get; init; }

    /// <summary>
    /// <c>none</c> (the default); <c>interactive</c> numbers every focusable or pressable node on the image
    /// and lists them; <c>all</c> every node with a role or test ID.
    /// </summary>
    public string? Annotate { get; init; }

    /// <summary>Only this element.</summary>
    public Selector? Selector { get; init; }

    /// <summary>Times the app's pixel scale (default 1).</summary>
    public float? Scale { get; init; }

    /// <summary>Wait for the app to be idle first (default true), so nothing's caught mid-animation.</summary>
    public bool? WaitIdle { get; init; }
}

/// <summary>A numbered node on an annotated screenshot.</summary>
public sealed record ScreenshotMark(int Mark, int Id, string? Role, string? Label, string? TestId, RectValue Bounds, PointValue? Tap);

/// <summary>What <c>ui.screenshot</c> wrote.</summary>
public sealed record ScreenshotInfo
{
    /// <summary>The PNG.</summary>
    public string Path { get; init; } = "";

    /// <summary>Its width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Its height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Pixels per logical point: divide a pixel position by it for <c>ui.tapAt</c>.</summary>
    public float PixelScale { get; init; }

    /// <summary>Where the image's top left is, in logical points (for a crop).</summary>
    public PointValue Origin { get; init; } = new(0, 0);

    /// <summary>The marks drawn, for an annotated screenshot.</summary>
    public ScreenshotMark[]? Marks { get; init; }
}

/// <summary>What <c>app.info</c> says.</summary>
public sealed record AppInfo
{
    /// <summary>The app.</summary>
    public string? App { get; init; }

    /// <summary>The instance, when it's served.</summary>
    public string? Instance { get; init; }

    /// <summary>Its process.</summary>
    public int Pid { get; init; }

    /// <summary>Its window's content on the screen, in logical points (or just its size, headless).</summary>
    public RectValue Window { get; init; } = new(0, 0, 0, 0);

    /// <summary>Pixels per logical point.</summary>
    public float PixelScale { get; init; }

    /// <summary>Whether it runs without a window.</summary>
    public bool Headless { get; init; }

    /// <summary><c>real</c> or <c>fixed</c>.</summary>
    public string Clock { get; init; } = "real";

    /// <summary>Its frame count.</summary>
    public long Frame { get; init; }

    /// <summary>Its UI time, in seconds.</summary>
    public double Time { get; init; }

    /// <summary>Whether it's idle.</summary>
    public bool Idle { get; init; }

    /// <summary>The focused node's id, or 0.</summary>
    public int FocusedId { get; init; }

    /// <summary>The interaction log, if it writes one.</summary>
    public string? Log { get; init; }

    /// <summary>The agent protocol version.</summary>
    public int Protocol { get; init; } = AgentProtocol.Version;
}

/// <summary>What <c>app.idle</c> says.</summary>
public sealed record IdleResult
{
    /// <summary>Whether it's idle.</summary>
    public bool Idle { get; init; }

    /// <summary>How many frames it waited.</summary>
    public long Frames { get; init; }

    /// <summary>What kept it busy, if it still is.</summary>
    public string[] Busy { get; init; } = [];

    /// <summary>What's moving that doesn't count: spinners, a caret.</summary>
    public string[] Continuous { get; init; } = [];

    /// <summary>What's waiting to happen: a tooltip's delay, a snackbar's timeout.</summary>
    public string[] Timers { get; init; } = [];
}

/// <summary>What <c>app.step</c> did.</summary>
public sealed record StepResult(long Frame, double Time);

/// <summary>What an action on an element did.</summary>
public sealed record ActionResult
{
    /// <summary>The element.</summary>
    public NodeRef? Target { get; init; }

    /// <summary>Where the input went.</summary>
    public PointValue? At { get; init; }

    /// <summary>How many frames it took.</summary>
    public long Frames { get; init; }

    /// <summary>Whether the app was idle when it answered.</summary>
    public bool Idle { get; init; }

    /// <summary>For typing: the field's value after.</summary>
    public string? Value { get; init; }

    /// <summary>Which node has focus after.</summary>
    public int? FocusedId { get; init; }

    /// <summary>For scrolling: where the area is after.</summary>
    public ScrollValue? Scroll { get; init; }
}

/// <summary>Where a scroll area is.</summary>
public sealed record ScrollValue(PointValue Offset, PointValue Max, SizeValue Content, SizeValue Viewport, bool Animating);

/// <summary>Params for <c>log.subscribe</c>.</summary>
public sealed record LogSubscribeParams
{
    /// <summary>Replay entries after this sequence number first; -1 for none (the default: from now).</summary>
    public long? Since { get; init; }

    /// <summary>Only these sources, comma-separated: <c>human</c>, <c>agent</c>, <c>test</c>, <c>app</c>.</summary>
    public string? Src { get; init; }

    /// <summary>Only these kinds, comma-separated.</summary>
    public string? Kinds { get; init; }
}

/// <summary>What <c>log.subscribe</c> started.</summary>
public sealed record LogSubscription(string Sub, long Seq, string? Path);

/// <summary>Params for <c>log.unsubscribe</c>.</summary>
public sealed record LogUnsubscribeParams
{
    /// <summary>The subscription.</summary>
    public string? Sub { get; init; }
}

/// <summary>Params for <c>log.note</c> and <c>log.tail</c>.</summary>
public sealed record LogNoteParams
{
    /// <summary>The note.</summary>
    public string? Text { get; init; }

    /// <summary>For <c>log.tail</c>: how many entries.</summary>
    public int? Count { get; init; }
}

/// <summary>What <c>log.note</c> wrote.</summary>
public sealed record LogNoteResult(long Seq);

/// <summary>A scroll area's position as <c>ui.inspect</c> writes it.</summary>
public sealed record InspectScroll
{
    /// <summary>How far it's scrolled.</summary>
    public PointValue? Offset { get; init; }

    /// <summary>How far it can go.</summary>
    public PointValue? Max { get; init; }

    /// <summary>The content's size.</summary>
    public SizeValue? Content { get; init; }

    /// <summary>The viewport's size.</summary>
    public SizeValue? Viewport { get; init; }

    /// <summary>Which ways it can scroll.</summary>
    public AxisFlags? CanScroll { get; init; }

    /// <summary>Whether it's moving by itself.</summary>
    public bool Animating { get; init; }
}

/// <summary>A yes or no for each axis.</summary>
public sealed record AxisFlags(bool X, bool Y);

/// <summary>A text selection, as character offsets.</summary>
public sealed record SelectionValue(int Start, int End);

/// <summary>
/// A node as <c>ui.inspect</c> and <c>ui.tree</c> write it: only the fields asked for are set.
/// </summary>
public sealed record InspectNode
{
    /// <summary>Its id.</summary>
    public int? Id { get; init; }

    /// <summary>Its role.</summary>
    public string? Role { get; init; }

    /// <summary>Its label.</summary>
    public string? Label { get; init; }

    /// <summary>Its test ID.</summary>
    public string? TestId { get; init; }

    /// <summary>What kind of node: box, text, editableText, scroll, image, canvas, grid, portal, root.</summary>
    public string? Kind { get; init; }

    /// <summary>Where it is, in logical window points.</summary>
    public RectValue? Bounds { get; init; }

    /// <summary>Its size.</summary>
    public SizeValue? Size { get; init; }

    /// <summary>Where it is in its parent.</summary>
    public PointValue? Position { get; init; }

    /// <summary>What of it can be seen, or null.</summary>
    public RectValue? Visible { get; init; }

    /// <summary>How much of it can be seen, 0 to 1.</summary>
    public float? VisibleRatio { get; init; }

    /// <summary>Its opacity with its ancestors'.</summary>
    public float? Opacity { get; init; }

    /// <summary>Whether any of it is on the window.</summary>
    public bool? OnScreen { get; init; }

    /// <summary>Where a tap lands on it.</summary>
    public PointValue? Tap { get; init; }

    /// <summary>Whether a tap can land on it.</summary>
    public bool? Hittable { get; init; }

    /// <summary>What covers it.</summary>
    public NodeRef? ObscuredBy { get; init; }

    /// <summary>For a scroll area: where it's scrolled.</summary>
    public InspectScroll? Scroll { get; init; }

    /// <summary>Its value.</summary>
    public string? Value { get; init; }

    /// <summary>Its description.</summary>
    public string? Description { get; init; }

    /// <summary>A heading's level.</summary>
    public int? HeadingLevel { get; init; }

    /// <summary>Whether it's disabled.</summary>
    public bool? Disabled { get; init; }

    /// <summary>Whether it's checked.</summary>
    public bool? Checked { get; init; }

    /// <summary>Whether it's selected.</summary>
    public bool? Selected { get; init; }

    /// <summary>Whether it's expanded.</summary>
    public bool? Expanded { get; init; }

    /// <summary>Whether it can take focus.</summary>
    public bool? Focusable { get; init; }

    /// <summary>Whether it has focus.</summary>
    public bool? Focused { get; init; }

    /// <summary>The text it shows.</summary>
    public string? Text { get; init; }

    /// <summary>For editable text: the selection.</summary>
    public SelectionValue? Selection { get; init; }

    /// <summary>For editable text: the placeholder.</summary>
    public string? Placeholder { get; init; }

    /// <summary>Its render node's type.</summary>
    public string? Type { get; init; }

    /// <summary>Whether it clips its children.</summary>
    public bool? Clips { get; init; }

    /// <summary>Whether it can be hit itself.</summary>
    public bool? HitTestVisible { get; init; }

    /// <summary>Whether it's transformed.</summary>
    public bool? Transformed { get; init; }

    /// <summary>Where it is on the screen.</summary>
    public RectValue? ScreenBounds { get; init; }

    /// <summary>Where it is in screenshot pixels.</summary>
    public RectValue? PixelBounds { get; init; }

    /// <summary>How many children it has, where the depth stopped.</summary>
    public int? ChildCount { get; init; }

    /// <summary>Its children, to the depth asked for.</summary>
    public InspectNode[]? Children { get; init; }
}

/// <summary>What <c>ui.tree</c> and <c>ui.inspect</c> answer.</summary>
public sealed record InspectDocument
{
    /// <summary>The frame it describes.</summary>
    public long Frame { get; init; }

    /// <summary><c>semantics</c> or <c>render</c>.</summary>
    public string Tree { get; init; } = "semantics";

    /// <summary>Always <c>logical</c>: window points.</summary>
    public string Coords { get; init; } = "logical";

    /// <summary>Pixels per point.</summary>
    public float PixelScale { get; init; }

    /// <summary>The window's content: where on the screen, and its size.</summary>
    public RectValue? Window { get; init; }

    /// <summary>The nodes asked for.</summary>
    public InspectNode[] Nodes { get; init; } = [];
}

/// <summary>Source-generated JSON for the automation's params and results.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(TreeParams))]
[JsonSerializable(typeof(InspectParams))]
[JsonSerializable(typeof(QueryParams))]
[JsonSerializable(typeof(QueryResult))]
[JsonSerializable(typeof(ElementActionParams))]
[JsonSerializable(typeof(TapAtParams))]
[JsonSerializable(typeof(KeyParams))]
[JsonSerializable(typeof(ScrollParams))]
[JsonSerializable(typeof(ScrollToParams))]
[JsonSerializable(typeof(DragParams))]
[JsonSerializable(typeof(WaitForParams))]
[JsonSerializable(typeof(IdleParams))]
[JsonSerializable(typeof(StepParams))]
[JsonSerializable(typeof(ScreenshotParams))]
[JsonSerializable(typeof(ScreenshotInfo))]
[JsonSerializable(typeof(AppInfo))]
[JsonSerializable(typeof(IdleResult))]
[JsonSerializable(typeof(StepResult))]
[JsonSerializable(typeof(ActionResult))]
[JsonSerializable(typeof(NodeRef))]
[JsonSerializable(typeof(NodeRef[]))]
[JsonSerializable(typeof(LogSubscribeParams))]
[JsonSerializable(typeof(LogSubscription))]
[JsonSerializable(typeof(LogUnsubscribeParams))]
[JsonSerializable(typeof(LogNoteParams))]
[JsonSerializable(typeof(LogNoteResult))]
[JsonSerializable(typeof(LogEntry))]
[JsonSerializable(typeof(LogEntry[]))]
[JsonSerializable(typeof(ExitResult))]
[JsonSerializable(typeof(InspectDocument))]
[JsonSerializable(typeof(BoolResult))]
[JsonSerializable(typeof(JsonElement))]
public sealed partial class AutomationJsonContext : JsonSerializerContext;
