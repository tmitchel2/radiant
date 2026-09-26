# Radiant UI: elements, components and the frame

`Radiant.UI.Core` is Radiant's declarative UI. You describe what the UI should be (elements) and
the framework keeps the live objects (state, layout, drawing) up to date with each description.

```csharp
public sealed record Counter(string Label) : Component
{
    public override Element Build(BuildContext context)
    {
        var count = context.UseState(0);
        return new Box
        {
            Layout = new LayoutStyle { Padding = Edges.All(12) },
            Background = Color.Parse("#eaddff"),
            CornerRadii = CornerRadii.All(12),
            OnClick = _ => count.Set(count.Value + 1),
            Children = [new TextBlock($"{Label}: {count.Value}")],
        };
    }
}

RadiantUI.Run(new Counter("Clicks"), new UIAppOptions { Title = "Counter" });
```

## Three trees

| Tree | What | Lifetime |
|---|---|---|
| **Elements** (`Element` records) | The description: props, children | Made fresh on every build; immutable |
| **Element nodes** (internal) | State (hooks), children, the element last built | As long as the element keeps matching |
| **Render nodes** (internal) | Layout (a Yoga node each), drawing, hit testing | As long as their host element's node |

- **Components** (`Component`, a record with `Build`) describe UI in terms of other elements. They
  leave nothing in the render tree.
- **Host elements** are what gets laid out and drawn: `Box`, `TextBlock`, `ScrollArea`, `Portal`,
  `EditableText` and `Canvas` (custom drawing with the renderer, as an escape hatch).
- **Structure:** `Fragment` groups elements, and `Provider<T>` passes a context value down.

## Reconciliation

On each build, a node's new child elements are matched to its existing children:
- **Keyed elements** (`Key = id`) match the child with the same key, wherever it is, so reordering
  a list keeps each item's state.
- **Unkeyed elements** match the child at the same position in the child list, if it has the
  same type. `null` entries hold their position, so `cond ? x : null` doesn't disturb later
  siblings.

A matched node takes the new element; the rest are mounted or unmounted. An element equal to the
last (records compare by value) isn't rebuilt and its subtree is skipped, unless its own state
changed.

## Hooks

`BuildContext` holds the hooks. Call them in the same order on every build, never conditionally;
a change of order throws.

| Hook | What it does |
|---|---|
| `UseState(initial)` | A `State<T>`; `Set`/`Update` rebuild the component (once per frame however often) |
| `UseRef(initial)` | A mutable box that doesn't rebuild |
| `UseMemo(compute, deps)` | Recomputed only when `deps` (a tuple, compared by value) changes |
| `UseEffect(effect[, deps])` | Runs after layout, children's before parents'. Its returned cleanup runs before the next run and on unmount |
| `Use(context)` | The nearest `Provider<T>` value; rebuilt when it changes, even past components that don't rebuild |
| `Watch(signal)` | A `Signal<T>`'s value; rebuilt when it changes |
| `UseTransition(target, duration[, easing, initial])` | A value that animates to each new target (floats, or any type with a lerp), rebuilding each frame while it moves |

## The frame

`UIRoot` runs one tree:
1. **Input** (`PointerDown`, `PointerMove`, `KeyDown`, `TextInput`, …) is dispatched against the
   last layout.
2. **`Update(size)`**:
   - rebuilds dirty components, parents first;
   - lays out with Yoga, which recomputes only what was marked dirty;
   - runs effects;
   - repeats if effects changed state.
3. **`Paint(renderer)`** draws. Nodes paint at absolute positions; only real transforms, clips and
   opacity push renderer state.

`RadiantUI.Run(element, options)` does all of that for a window. Rebuilding one leaf in a
5,000-node tree and laying out again takes about 0.3 ms.

## Events

- **Routing:** pointer and key events route like the DOM's. They go root to target through
  `…Capture` handlers, then target to root through the others, and stop when `Handled` is set.
- **Hit testing:** later siblings are on top, and hits follow transforms and clips.
  `HitTestVisible = false` lets the pointer through.
- **Presses:** a press is captured, so its moves and release go to where it began. `OnClick` fires
  on the deepest box that both the press and the release were over; `ClickCount` counts double
  clicks.
- **Hover:** `OnPointerEnter` and `OnPointerLeave` follow the pointer and don't bubble.
- **Focus:** `Focusable` boxes take focus when pressed (no focus ring) or by Tab and Shift+Tab in
  tree order (with a ring: `IsFocusVisible`). Key and text events go to the focused box and
  bubble up.
- **Cursor:** `Box.Cursor` sets the pointer's shape over a box (null inherits). `UIRoot.Cursor`
  is the deepest hovered box's, or the pressed box's while a press is held, and `RadiantUI.Run`
  shows it through the platform.
- **Text input clients:** a focused text field sets `UIRoot.TextInputClient`. Typed text and
  input method compositions then go to it rather than to text events (see
  [platform.md](platform.md#text-input-and-input-methods)).

`RadiantUI.Run` makes the window's platform with `UIAppOptions.Platform` and provides it to
components through `PlatformContext` (`context.UsePlatform()`): clipboard, dialogs, appearance,
cursors and text input. See [platform.md](platform.md).

## Scrolling

`ScrollArea` is a viewport onto content that can be larger than it.
- **Layout:** the content is laid out in a node inside the viewport, and Yoga's scroll overflow
  leaves it unconstrained along the scrolling axes. It grows to fill the viewport when shorter.
  As in CSS, the area shrinks to fit rather than growing to its content.
- **Scrolling:** the wheel scrolls through `ScrollController` (Radiant's scroll physics: bounce,
  momentum, snapping, animated `ScrollTo`). `UIRoot.Advance(seconds)` moves it each frame.
- **Nesting:** an area passes the wheel to the one enclosing it when it can't scroll further
  that way.
- **Position:** the scroll position survives rebuilds; pass a `Controller` to set or read it.

## Portals, refs and semantics

- **`Portal`** shows its children above everything, in the root's coordinates, wherever it is in
  the tree. This is how menus, popovers and dialogs escape their parents' clips and layout.
  - Later portals are above earlier ones.
  - An empty part of the layer lets the pointer through.
  - Events bubble along the *element* tree, as React's do, so a key pressed in a menu reaches the
    component that opened it.
- **`ElementRef`** (`Box.Ref`) gives code a box's bounds in root coordinates as of the last layout,
  and `Focus()`. Anchored popovers position themselves from it in an effect.
- **Semantics:** `Box.Semantics` gives a box a role, label, value and states.
  `UIRoot.GetSemantics()` builds the accessibility tree:
  - Boxes with semantics or focus appear, and so does text.
  - Other boxes pass their children up.
  - A control without a label is named by the text inside it.

## Style facets

A facet is a group of related props, such as corner shape, outline or background colour,
declared once as a `[StyleFacet]` interface. Components choose which facets they expose, and
which of them feed each of their parts, as Destash components do.

```csharp
[StyleFacet]
public interface IHasCornerShape
{
    CornerShapeRole? CornerShape { get; init; }
}

[ForwardFacets(typeof(PressableSurface), "Container", typeof(IHasCornerShape), typeof(IHasOutline))]
public sealed partial record SurfaceButton : Component, IHasCornerShape, IHasOutline
{
    public override Element Build(BuildContext context) =>
        ForwardContainer(new PressableSurface { Children = [...] }) with { ShowSurface = true };
}
```

`Radiant.Generators` (a Roslyn incremental generator; reference it as an analyzer) writes:
- **Facet properties:** every facet property the partial record doesn't declare itself.
- **Forwarders:** `Forward{Name}(target)`, which returns the target with the record's facet values
  copied on. A null value keeps the target's own, and a `with` afterwards overrides what was
  forwarded.
- **Errors for mistakes:**

  | Id | Mistake |
  |---|---|
  | RAD002 | Forwarding a facet that either side lacks |
  | RAD004 | A facet record that isn't partial |
  | RAD005 | A facet type that isn't a record |
  | RAD006 | A facet member that isn't a `get; init;` property |
  | RAD007 | Forwarding an interface that isn't a facet |

## Animation

- **`UseTransition`** animates a value towards each new target on the root's frames: from where it
  is, along an `Easing` (Material's curves live in `Radiant.Animation.Easing`).
- **`Presence(visible, progress => element)`** keeps content mounted while it animates out, then
  removes it. It animates in when mounted, too.
- **`UIRoot.AddTicker`** is the general hook for anything that animates itself; theme transitions
  use it.

## Editing text

- **`TextEditState`:** text, a `TextSelection` (anchor, focus, affinity) and the input method's
  composing range.
- **`TextEditing`:** the edits as pure functions over the state. Typing replaces the selection or
  composing text. Deletion goes by grapheme, word or line. Movement goes through the laid-out
  `Paragraph`, so arrows follow bidi text on screen and up and down keep their column. There are
  word and line selection, and input-method composition.
- **`EditableText`:** a host element that draws the text with its selection, composing underline
  and caret. A single line scrolls sideways to keep the caret in view. `EditableTextRef` gives hit
  testing and the caret's bounds.
- **`TextInput`:** a controlled component (`State` in, `OnChange` out) with a Mac text field's
  behaviour:
  - pointer caret placement, drag selection, double-click for a word and triple-click for a line;
  - arrows, Option for words, Command for line and document ends, Shift to extend;
  - Backspace and Delete by character, word or to the line start;
  - Command-A, C, X, V, Z and Shift-Z, with runs of typing undone at once;
  - Enter submits a one-line input or breaks a multiline one;
  - a blinking caret.

  Copy and paste use the platform's clipboard (`context.UsePlatform().Clipboard`). While focused,
  the input is the platform's text input client, so input methods compose in place with their
  candidate window at the caret (see [platform.md](platform.md)).

## Not yet

- **Host elements:** `Image`.
- **Overlay behaviour:** anchoring, dismissing and focus traps come with the P8 primitives.
- **Scrolling:** dragging the scroll thumb, and keyboard scrolling.
- **Commands:** commands and shortcuts.
- **Accessibility:** the platform bridge for semantics comes later in P7 (the platform's other
  services are in [platform.md](platform.md)).
- **Theming:** P6.
