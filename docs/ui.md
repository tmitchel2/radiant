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
- **Host elements** are what gets laid out and drawn: `Box`, `TextBlock` and `ScrollArea` for now.
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

## Not yet

- **Host elements:** `Portal` and overlays, `Image`, and a canvas for custom drawing.
- **Scrolling:** dragging the scroll thumb, and keyboard scrolling.
- **Semantics and commands:** a semantics tree for accessibility, commands and shortcuts, and
  `Presence` for exit animations.
- **Styling and theming:** the style facets and generated forwarders (P5), and theming (P6).
