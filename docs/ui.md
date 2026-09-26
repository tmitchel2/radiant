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
  `EditableText`, `Image` (cover, contain or fill, with rounded corners and alt text) and `Canvas`
  (custom drawing with the renderer, as an escape hatch).
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

## Idle

`RadiantUI.Run` draws a frame only while the tree has something to do (`UIRoot.NeedsUpdate`: a
rebuild, an effect, a scroll or a ticker). Otherwise the window waits for input
(`RadiantApplication.NeedsFrame`), and anything that asks for a frame without input wakes it
(`UIRoot.FrameRequested` → `RadiantApplication.RequestFrame`). An idle window uses no CPU. The
first step after waiting is capped at 50 ms, so an animation starting then doesn't finish at
once. Keep components to this: register tickers only while something moves.

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
- **Shortcuts:** `context.UseShortcut(KeyChord.Command(KeyCode.K), open)` runs while its component
  is mounted, when the key reaches no focused handler (or nothing is focused). A deeper
  component's shortcut takes the chord from one above it (a dialog's over the app's).
  `KeyChord` prints as the platform writes it ("⌘K", "Ctrl+K").
- **Commands:** `context.UseCommand(new Command("save", "Save") { Shortcut = …, Menu = "File", Run = … })`
  registers something the user can do while its component is mounted (see [Commands](#commands)).
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
- **Scroll bars:** the indicators can be dragged, and pressing their track jumps the thumb
  there. The 12 px strip along the edge belongs to the scroll area while it can scroll that way,
  even over content (`RenderNode.ClaimsPoint`), so a drag there never clicks what's under it.

## Grids

`Grid` lays its children out in equal columns, row after row: a fixed `Columns` count, or as
many columns as fit `MinColumnWidth` (capped by `MaxColumns`), with `ColumnGap` and `RowGap`.
Unlike a wrapping flex row, every cell is the column's width, so a short last row lines up with
the rows above. Cells in a row are as tall as the tallest, unless they set their own height.

```csharp
new Grid { MinColumnWidth = 220, MaxColumns = 4, ColumnGap = 16, RowGap = 16, Children = cards }
```

Yoga has no grid, so the grid is a wrapping row whose children are given the column width. The
width depends on the grid's own laid-out width, so it's worked out after layout, and when it
changed the root lays out again. Each layout reuses the last width, so a second pass happens
only when the grid's width changes (a resize) or the column count does. A child's own width and
flex sizes are overridden.

## Commands

A `Command` names something the user can do: its id, title, `Run`, and optionally a `Shortcut`,
the `Menu` it's on, a `Group`, an icon and keywords, whether it's `Enabled` and whether it's
`Checked`. `context.UseCommand(command)` registers it in `UIRoot.Commands` while the component is
mounted and keeps it current with every build, so it can say it's disabled or ticked now:

```csharp
context.UseCommand(new Command("save", "Save")
{
    Menu = "File",
    Shortcut = KeyChord.Command(KeyCode.S),
    Enabled = document.IsDirty,
    Run = () => document.Save(),
});
```

- **Shortcuts:** an enabled command's shortcut runs it, as `UseShortcut` would.
- **Listing:** `context.UseCommands()` returns the registered commands and rebuilds when they
  change: what they run changing with each build doesn't count. `CommandPalette` takes that list,
  and `CommandMenuBar` makes the menu bar from it (see [components.md](components.md)).
- **Order:** commands are listed in tree order (a parent's before its children's), each
  component's in the order it registered them.
- **Overriding:** where two share an id, the one deeper in the tree is the one listed and run: an
  editor's Copy over the app's, for as long as the editor is mounted.
- **Running by id:** `UIRoot.Commands.Execute("save")` runs an enabled command.

## Direction

A UI reads left to right unless it's wrapped in `Directionality`:

```csharp
new Directionality(TextDirection.RightToLeft, app)
```

- **Layout mirrors.** `Edges` are logical: `Start` is the left in a left-to-right UI and the right
  in a right-to-left one, and the same goes for insets and margins. Rows run from the start, and
  `Justify.FlexStart` and `Align.FlexStart` in a row mean the start. `LayoutStyle.Direction` sets a
  subtree's direction, and Yoga passes it down.
- **Text takes the direction it's laid out in.** Text (and a text field's text) in a right-to-left
  layout is a right-to-left paragraph, so mixed text orders right to left and
  `TextAlignment.Start` means the right; a single-line field keeps its text against the right edge
  and scrolls the other way. Elsewhere text detects its direction from its first strong
  character, and `TextBlock.Direction` overrides both.
- **Portals take the direction where they are** in the element tree, not the root's.
- **Scroll areas mirror.** A horizontal area starts at its right edge and scrolls leftwards; its
  offsets still count from the start. The vertical bar is on the left.
- **Controls follow.** Sliders fill from the right, and Left and Right swap for sliders, tabs,
  toolbars, menus, trees, grids, calendars, carousels and splitters (`KeyCode.ForDirection` does
  the swap). Dragging a splitter or a column edge towards the end grows what it sizes. Anchored
  content mirrors its side and alignment, and sheets slide in from the mirrored edge.
- **Icons that point along the line mirror** (back and forward arrows, chevrons, first and last
  page): `SurfaceIcon` draws the opposite icon, unless `MirrorInRightToLeft` is off.
- **Points stay physical.** Pointer positions and bounds are measured from the left, whatever the
  direction. Something placed at one (a popover, a context menu, a tab indicator) uses
  `Edges.Physical(left, top, right, bottom, rightToLeft)`, which swaps sides in a right-to-left
  layout.
- **Components read it** with `context.UseDirection()` (null if nothing set it) or
  `context.UseRightToLeft()`.
- **Some things don't mirror.** Charts, the colour picker's plane and strip, and code stay left
  to right in any language: set `LayoutStyle.Direction` to `LeftToRight` on them. The gallery's
  `--rtl` flag shows every page right to left.

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
  - Text with a `HeadingLevel` is a heading (`SurfaceText.HeadingLevel` in components).

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
  forwarded. A facet whose type has a `T Merge(T over)` method is *layered*: the value set on the
  record goes over the target's own rather than replacing it. `LayoutStyle` (and `Edges`) merge
  property by property, so `new SurfaceButton("Sign in") { Layout = new() { AlignSelf = Align.Stretch } }`
  stretches the button and keeps its height, padding and centring.
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

- **Overlay behaviour:** anchoring, dismissing and focus traps come with the P8 primitives.
- **Scrolling:** keyboard scrolling.
- **Accessibility:** the platform bridge for semantics comes later in P7 (the platform's other
  services are in [platform.md](platform.md)).
- **Theming:** P6.
