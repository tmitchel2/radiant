# Radiant Layout & Styling

How elements are sized and placed, and where their look comes from. The retained widget tree
that this document used to describe (`UIElement`, `UIManager`, `Stylesheet` and the widgets built on
them) is gone: the declarative UI in `Radiant.UI.Core` replaced it (see [ui.md](ui.md)).

## Layout

Layout is flexbox, from Meta's Yoga (the [`Yoga.Net`](https://github.com/chenrensong/Yoga.Net)
package, MIT). Radiant exposes none of Yoga's types; everything goes through `Radiant.Layout`.

- **`LayoutStyle`** (in `Radiant`) is an element's layout: direction, wrapping, justify and align,
  grow, shrink and basis, width and height with their minimums and maximums, margin, padding,
  inset (with absolute positioning), row and column gaps, and aspect ratio. Every property is unset
  by default, so `default(LayoutStyle)` leaves Yoga's defaults. `Dimension` lengths are points,
  percentages or auto; `Edges` holds four of them: start, top, end and bottom. Start and end
  follow the layout's direction (`LayoutStyle.Direction`, or `Directionality` for a whole UI; see
  [ui.md](ui.md#direction)), so a right-to-left UI mirrors without changing any layout.
- **Merging:** `LayoutStyle.Merge` (and `Edges.Merge`) lays one style's set properties over
  another's. Components use it so a caller's layout adds to theirs: a button told to stretch keeps
  its padding and height.
- **Hugging:** `AlignSelf = Align.Hug` keeps an element to its own size: it's placed as its
  parent's `AlignItems` places items, except that where they'd stretch, it sits at the start.
  Yoga has no such value, so the root keeps the hugging nodes and sets each one's alignment from
  its parent's before layout. Tags and badges hug, so they neither stretch across a column nor
  leave a row's centring.
- **Render nodes keep their Yoga nodes.** Each host element's render node has one Yoga node for its
  life, so a rebuild only restyles what changed and Yoga only redoes dirty subtrees (`YogaStyle`
  maps a `LayoutStyle` onto a node). Text measures itself through Yoga's measure function, shaped
  once and laid out again only at a new width.
- **Grids:** Yoga has no grid, so `Grid` lays its children out in equal columns (a count, or as
  many as fit a minimum width) by sizing them after layout and laying out again when the width
  changes (see [ui.md](ui.md#grids)).
- **Scroll areas** lay their content out unconstrained along the scrolling axes (see
  [scrolling-and-gestures.md](scrolling-and-gestures.md)).

## Styling

There's no stylesheet. A component's look comes from:

- **The theme** (`Radiant.Theming`): colour roles resolved from a seed, shape, type, elevation,
  motion and state-layer scales, read with `context.UseTheme()` (see [theming.md](theming.md)).
- **Surface state:** the colours in force at a point in the tree, inherited down it, so content
  knows what's readable on the surface under it.
- **Style facets:** groups of props (corner shape, outline, background colour …) declared once as
  `[StyleFacet]` interfaces and forwarded between components by generated code (see
  [ui.md](ui.md#style-facets) and [components.md](components.md)).

## SDF-shape rendering

`Renderer2D` gains one **batched SDF-shape pipeline** that draws every analytic 2D shape — rounded
rectangle (per-corner radii), disc, ring, arc and segment — from a single shader that dispatches
per fragment on a shape kind. Like every pipeline it has its own vertex list, and its draws are
interleaved with the others in the order they are made (see [rendering.md](rendering.md)):

```csharp
renderer.DrawRoundedRectFilled(x, y, w, h, radius, fill);
renderer.DrawRoundedRect(x, y, w, h, radius, borderWidth, fill, border);   // fill + border in one quad
renderer.DrawRoundedRectFilled(x, y, w, h, CornerRadii.Top(8f), fill);     // per-corner (tabs/cards)
renderer.DrawDisc(center, radius, fill);                                   // crisp AA circle
renderer.DrawRing(center, outerRadius, innerRadius, fill);                 // progress rings, radio outlines
renderer.DrawSegment(a, b, width, color);                                  // an anti-aliased line, any angle
```

Each fragment evaluates the matching signed-distance function and derives analytic
anti-aliasing from `fwidth`, which keeps the edge one pixel wide at any scale; edges then blend as
they would in sRGB, so thin borders keep their weight round their corners. Radii are clamped to
half the shorter side; `radius 0` gives sharp corners; a transparent `fill` yields a border-only
stroke. Adding a shape is a new `case` in `ShaderLibrary.SdfShapeShader` and a thin `Draw*` method.
