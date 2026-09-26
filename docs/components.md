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
