# Radiant Layout & Styling

Flexbox layout + a CSS-like styling layer for Radiant's retained-mode widget tree. Avalonia-style:
declarative styling of *our own* widgets, **not** an HTML/CSS document renderer.

- **Layout** — a vendored pure-C# port of Meta's Yoga engine (flexbox + grid) drives positions/sizes.
- **Styling** — predicate selectors + a small paint-only box model resolve each widget's colours/border.
- **Rendering** — an SDF rounded-rectangle pipeline in `Renderer2D` paints the box model.

All three are **opt-in and additive**: untouched widgets lay out and render exactly as before.

## Layout

### Opt-in model
Layout participation is decided at the **root**. Set `UIElement.ParticipatesInLayout = true` on a
top-level element and the engine lays out its *entire* subtree from each element's `LayoutStyle`.
Everything else keeps its manually assigned `Position`/`Size`.

```csharp
panel.ParticipatesInLayout = true;
panel.Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, Padding = Edges.All(8f), ColumnGap = 8f };
sectionList.Layout = new LayoutStyle { Width = 200f };       // fixed
contentHost.Layout = new LayoutStyle { FlexGrow = 1f };      // fills the rest
```

### `LayoutStyle`
A `readonly record struct` of flexbox + box-model inputs. Every property is **unset by default**
(nullable enums; `Dimension`/`Edges` carry an `Undefined` sentinel), so `default(LayoutStyle)`
applies no overrides and the node keeps Yoga's defaults. Lengths use `Dimension` — `12f` (points,
implicit), `Dimension.Percent(50)`, or `Dimension.Auto`. Edges use `Edges.All(..)`/`Edges.Symmetric(..)`
or the 4-arg `(Left, Top, Right, Bottom)` ctor.

### How it runs
`YogaLayoutEngine` builds a **transient** Yoga tree each pass, runs `CalculateLayout`, writes the
computed rects back into `Position`/`Size`, then frees the tree — Yoga nodes never outlive a pass, so
they don't couple to widget lifetime. The root's own `Position`/`Size` are owned by the caller (window
anchoring) and used as the origin + available space; only descendants are written back.

Two entry points:
- `UIManager.Layout(viewport)` — lays out every opted-in, visible top-level element. Call **before**
  `Update` in the host loop: **Layout → Update → Draw**.
- `YogaLayoutEngine.CalculateRoot(element, viewport)` — lays out one root directly (used by
  `SettingsShell`, which sets its panel rect by anchoring and then lays out the body synchronously).

### Text leaves — `ILayoutMeasurable`
A content-sized leaf (e.g. `Label`) implements `ILayoutMeasurable.MeasureContent(availableWidth)`;
the engine registers a Yoga measure callback that defers to it, so the widget keeps its font private.
Yoga rounds results to the pixel grid (default point scale 1.0), so expect ≤1px differences from a
raw `MeasureTextWidth`.

### Scroll regions — `ILayoutBoundary`
A container that implements `ILayoutBoundary` (e.g. `ScrollPanel`) is **sized and positioned** by flex
like any item, but the walk **stops at it** — its children keep their own positioning scheme. This is
what lets a scroll region participate in a flex layout without the engine fighting its manual,
scroll-shifted child positions.

## Styling

### Predicate selectors (LINQ-style)
A selector is just `Func<UIElement, bool>` — no grammar, no parser, fully type-safe and composable.
Build them with `Selectors` and compose with `And`/`Or` (or bare `&&`/`||`):

```csharp
var sheet = new Stylesheet()
    .Add(Selectors.OfType<Button>(), new Style { BackgroundColor = grey })
    .Add(Selectors.OfType<Button>().And(Selectors.Hovered), new Style { BackgroundColor = blue })
    .Add(Selectors.Class("danger"), new Style { BorderColor = red, BorderWidth = 2f });
uiManager.Stylesheet = sheet;
```

`Selectors` provides `OfType<T>()`, `Class(name)`, `Any()`, and the pseudo-class selectors
`Hovered`/`Pressed`/`Disabled`/`Focused` (which read `UIElement.CurrentPseudoState`).

### `Style` (paint-only)
A `readonly record struct` with nullable `BackgroundColor`, `BorderColor`, `BorderWidth`,
`BorderRadius`, `TextColor`, `TextScale`. Scope is intentionally **paint-only** — layout stays on
`LayoutStyle` so the style pass and layout pass stay decoupled (see deferred items).

### Cascade
`StyleResolver.Resolve(element, stylesheet)` folds every matching rule in **cascade order**, then
layers the element's inline `UIElement.Style` on top (inline always wins). There is **no CSS
specificity** — predicates are opaque, so precedence is:

1. ascending `StyleRule.Priority`, then
2. stylesheet insertion order (later wins) for equal priority,
3. inline `Style` last.

Merging is per-property: a rule only overrides the properties it actually set. `UIManager.Draw`
resolves the whole tree into `UIElement.ResolvedStyle` each frame (after `Update`, so pseudo-state is
current); widgets read `ResolvedStyle` and fall back to their built-in `UIColors` for unset properties
— so an unstyled tree renders identically.

### Pseudo-state & classes
`UIElement.CurrentPseudoState` (a `[Flags] PseudoState`) reports `Disabled` by default; interactive
widgets override (e.g. `Button` adds `Hover`/`Active`). `UIElement.Classes` is a mutable string set
(`element.Classes.Add("primary")`), distinct from the single-token `Tag`.

## Rounded-rect rendering

`Renderer2D` gains an SDF rounded-rectangle pipeline (mirrors the MSDF pipeline: separate vertex list,
per-range scissor, emitted before MSDF text so backgrounds sit under text):

```csharp
renderer.DrawRoundedRectFilled(x, y, w, h, radius, fill);
renderer.DrawRoundedRect(x, y, w, h, radius, borderWidth, fill, border); // fill + border in one quad
```

The fragment shader evaluates a rounded-box signed-distance function and derives 1px analytic
anti-aliasing from `fwidth`, so corners stay crisp at any scale. `radius` is clamped to half the
shorter side; `radius 0` gives sharp corners; a transparent `fill` yields a border-only stroke. The
WGSL lives in `ShaderLibrary.RoundedRectShader` and is validated by naga at pipeline creation.

`Panel` and `Button` consult `ResolvedStyle.BackgroundColor`/`BorderColor`/`TextColor` (falling back to
their existing colours); wider adoption of `DrawRoundedRect` for the box model can follow as widgets
opt in.

## Vendored engine

The flexbox engine is a vendored copy of [`chenrensong/Yoga.Net`](https://github.com/chenrensong/Yoga.Net)
(MIT) under `src/Yoga.Net` — see `src/Yoga.Net/VENDOR.md` for the pinned commit, local modifications,
and re-vendoring steps. Its 833 upstream tests run in CI (`src/Yoga.Net.Tests`). Radiant exposes none
of Yoga's types publicly; everything goes through `Radiant.Layout`.

## Manual visual check

Headless tests cover layout math, style resolution, rounded-rect geometry, and (via naga) shader
validity. The on-screen result needs a display session — drive the running demo through the agent
control interface:

```bash
dotnet run --project src/Dynamis -- --name layout-check
# open Settings (top-right gear), resize the window, and screenshot:
dotnet run --project src/Dynamis.Cli -- instance send layout-check render.screenshot \
    --params '{"path":"settings.png","width":1280,"height":720}'
```

The migrated `SettingsShell` should lay out (section list + growing content host), reflow on resize,
and the content host should still scroll.

## Deferred — why deferred + trigger to revisit

| Item | Why deferred | Trigger to revisit |
|---|---|---|
| Real CSS-text parsing (e.g. ExCSS) | Consumers are C#; a fluent/predicate API is AOT-clean and refactor-safe | Non-C# authors need to edit styles without recompiling |
| Stylesheet-driven layout (`Style` carrying `LayoutStyle`) | Keeps the style pass and layout pass decoupled (style would have to run before layout) | A real need to set padding/flex from a stylesheet rule |
| `:focus` pseudo-state + focus management | No focus model exists in the widget tree yet | Keyboard navigation / focusable widgets land |
| `box-shadow`, gradients | Need a blurred-SDF / extra pass or gradient param block | A design spec calls for elevation or gradients |
| `ScrollPanel` as a *full* layout boundary (laying out its children in content space) | First cut treats it as a leaf (children stay manually positioned); sufficient for `SettingsShell` | Nested flex content inside a scroll region is needed |
| Yoga-tree caching / incremental layout | Rebuilding per frame is fine at panel scale | Measured frame cost, or trees beyond ~500 nodes |
| Style inheritance / `!important` | Flat cascade covers current needs (descendant/ancestor matching is already expressible as a predicate that walks parents) | Cascade expressiveness demand |
| `Renderer2D` no-clip fast path skips rounded + MSDF draws | Matches existing MSDF behaviour; the app always uses the clip-aware `BeginFrame` | A consumer needs the no-clip path to draw text/rounded rects |
