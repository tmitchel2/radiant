# Radiant Theming

`Radiant.Theming` turns a `Theme` record into every colour, radius, text style, shadow and
timing an app uses. A `ThemeController` swaps themes at runtime, animating if asked.

```csharp
var themes = new ThemeController(new Theme
{
    Colors = new ThemeColors { Seed = Color.Parse("#0061a4"), Variant = Variant.Vibrant },
    Shape = new ShapeScale().Scaled(1.5f),
});
RadiantUI.Run(new ThemeProvider(themes, new App()));

// Later, from anywhere:
themes.Set(themes.Theme with { Colors = themes.Theme.Colors with { IsDark = true } }, TimeSpan.FromMilliseconds(300));
```

## What a theme holds

| Part | Type | Default |
|---|---|---|
| Colour | `ThemeColors` | Seed `#6750a4`, tonal spot, light, contrast 0, 2021 spec; success, warning and info colours harmonised to the seed |
| Shape | `ShapeScale` | Radii 4/8/12/16/20/24/28/32/48 and full, for `CornerShapeRole` |
| Type | `TypeScale` | Material 3's 15 styles in Inter plus Destash's extra steps, and `Code` in JetBrains Mono |
| Elevation | `ElevationScale` | Levels 0–5, each a key shadow and an ambient shadow |
| State layers | `StateLayerOpacities` | Hover .08, focus .10, pressed .10, dragged .16; disabled .12 container, .38 content |
| Motion | `MotionScheme` | Short/medium/long durations, standard/enter/exit easings, reduced motion |
| Density | `int` | 0 (each step −4 px on controls) |
| Spacing | `SpacingUnit` | 4 px |

## Colour: surfaces, not swatches

Components don't pick colours. They say what *surface* they are, and the surface state that
flows down the tree works out what's readable on it. This is ported from Destash.

- **Families:** a `SurfaceName` (`Surface`, `SurfaceContainerHigh`, `Primary`, `SecondaryFixed`,
  `Error`, `Success`, …) is a family of four colours: the colour, content on it (**on**), the
  quieter **container**, and content on the container. They come from Radiant.ColorSystem's
  scheme for the seed, so every pair is readable at any contrast level, light or dark.
- **Custom families:** `Success`, `Warning` and `Info` are the primary roles of a scheme seeded
  with the harmonised custom colour, so they follow variant, contrast and spec version like the
  rest.
- **Surface state:** a `SurfaceState` holds three `SurfaceRoleState`s: surface, content, and
  focused content. Each is a family plus on and container flags and an opacity (`Legibility`:
  full 1, high .87, medium .6, low .38, very low .12).
- **Changing it:** a component applies its `SurfaceChange` to the state it inherits
  (`SurfaceState.With`, Destash's `withBackgroundColor`). The change can set a surface family,
  set legibility, toggle on or container, set a content family, or show error or disabled. For
  example, `new SurfaceChange { Surface = SurfaceName.Primary }` makes a filled button: the surface
  is primary, and its content is on-primary.
- **Resolving it:** `ResolvedTheme.Get(role)` turns a role state into a colour, and the opacity
  applies.

## Runtime

- **Reading the theme:** `ThemeProvider` gives its subtree the controller's current
  `ResolvedTheme` (`context.UseTheme()`) and ticks transitions on the UI's frames.
  `context.UseSurface()` reads the surface state; surface components provide a changed one to their
  children through `ThemeContexts.Surface`.
- **Transitions:** colours mix in OKLab, so blue to orange doesn't pass through grey, and radii
  interpolate. Type, motion and density switch at once, so text doesn't reflow every frame. With
  reduced motion, changes are immediate.
- **Resolved values:** `ResolvedTheme` computes every role once when resolved, so a lookup is an
  array index.
