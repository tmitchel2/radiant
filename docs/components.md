# Radiant Components

`Radiant.Components` is the component catalogue, built on `Radiant.UI.Core` (elements and hooks),
`Radiant.Theming` (the theme and surface state) and `Radiant.Generators` (style facets).

## Facets

Components share props through style facets, generated onto each component by
`Radiant.Generators` (see [ui.md](ui.md#style-facets)):

| Facet | Props |
|---|---|
| `IHasBackgroundColor` | `SurfaceColor`, `SurfaceLegibility`, `SurfaceOnToggle`, `SurfaceContainerToggle`, `ContentColor`, `ContentLegibility`, `ContentOnToggle`, `ContentContainerToggle`, `ContentFocusedColor`, `ShowSurface`, `ShowError`, `ShowDisabled` |
| `IHasCornerShape` | `CornerShape` |
| `IHasElevation` | `Elevation` |
| `IHasOutline` | `ShowOutline`, `OutlineWidth`, `OutlineVariant` |
| `IHasText` | `Text`, `TextType` |
| `IHasPressable` | `OnPress` |
| `IHasLayout` | `Layout` |
| `IHasIcon` | `Icon`, `IconFilled`, `IconSize` |

A component forwards the facets each part needs with `[ForwardFacets]`. Facets set on the
component override its presets: `new SurfaceButton("Delete") { SurfaceColor = SurfaceName.Error }`
turns a filled button red.

## Components so far

| Component | What |
|---|---|
| `Surface` | Applies its background-colour props to the inherited surface state and draws the surface (colour, corners, elevation, outline); children inherit the new state |
| `PressableSurface` | A surface that presses: focus, pointer, Enter and Space; hover, focus and pressed state layers; disabled fades and stops responding; a button to assistive technology |
| `SurfaceText` | Text in the surface's content colour, in a type-scale step, with optional legibility |
| `SurfaceButton` | Filled, tonal, outlined, text and elevated buttons: a pill-shaped `PressableSurface` with a label |
| `Card` | Elevated, filled and outlined cards |
| `SurfaceIcon` | A Material Symbols icon in the content colour (decorative: hidden from assistive technology) |
| `IconButton` | Standard, filled, tonal and outlined icon buttons, named by a required label |

| `Checkbox` | Controlled check box: checked, indeterminate, error, disabled; the tick fades in |
| `Switch` | Controlled switch; the handle slides and grows (bigger still while pressed) |
| `Radio` | Controlled radio button; selecting calls `OnSelect`, and the dot grows in |

| `Divider` | A thin outline-variant line, across or down, optionally inset |
| `ListItem` | One-, two- or three-line rows with leading and trailing icon or text; pressable and selectable |
| `Menu` | Drops from an anchor: arrow keys and Tab move, a choice closes it, as do Escape and an outside press; icons, shortcuts, dividers, disabled items |
| `Dialog` | Modal: scrim, focus trap, Escape and scrim close (unless it must be answered), focus restored, fade and scale |
| `Tooltip` | After the pointer rests 600 ms, a small inverse-surface label beside its child |

Buttons take a leading icon (`Icon = "add"`). The selection controls are *controlled*, like
React's: they show the value they're given and report presses with the value they should become.
The whole row, label included, presses and takes focus, and the state layer is a 40 px circle
round the indicator.

## Icons

Icons are Material Symbols Rounded (Apache-2.0), drawn as text: an icon's name, set in
`FontLibrary.Icons`, becomes the icon through the font's ligatures. FILL, weight and optical size
are variable (`TextStyle.Variations`).
- **Embedded set:** Radiant embeds 272 common icons (408 KB). The list is `tools/icons/icons.txt`;
  `tools/icons/subset.sh` rebuilds the font from a pinned upstream commit.
- **All icons:** register the full font under `FontLibrary.Icons` for the rest.

## Primitives (`Radiant.Components.Primitives`)

These are the headless building blocks of overlays, like Radix's:

| Primitive | What |
|---|---|
| `DismissableLayer` | Calls `OnDismiss` on a press outside it (content it shows through a portal counts as inside) or Escape inside it |
| `FocusScope` | Moves focus in when shown, keeps Tab inside (`Box.TrapFocus`), gives focus back when it goes |
| `Anchored` | Floating content in a portal, placed against an `ElementRef`: side, alignment, offset, flip, shift, match width. It follows the anchor as it moves and stays hidden until placed |
| `AnchoredPlacement` | The placement maths on its own |

They rest on UI core hooks: `UIRoot.ObservePointerDown`, `SaveFocus`/`FocusSnapshot.Restore`,
`FocusFirst` and `Box.TrapFocus`.

## Colours are opaque

Legibility (text at 0.87, secondary text at 0.6) and state layers (hover at 0.08) are *mixed
into the surface colour in sRGB* by `ResolvedTheme.ContentColor` and `StateLayerColor`. They are
not drawn translucent. Material's opacities were designed for sRGB blending; the same opacity
blended in linear light makes 87% text look about 40% lighter. The surface state always knows
the colour underneath, so the result can be opaque, which is also better for text rendering.

## The gallery

`Radiant.Gallery` shows the first milestone, a vertical slice: a card with every button variant,
and a button that shuffles the theme (seed, variant, light or dark, corner scale), animating
everything to it.

```
dotnet run --project src/Radiant.Gallery                                   # in a window
dotnet run --project src/Radiant.Gallery -- --snapshot out.png --dark --scale 2   # to a PNG
```
