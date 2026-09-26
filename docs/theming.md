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

## Presets

`ThemePresets` holds ready-made themes: the same components, a different look each. Every part of
the theme below differs between them.

| Preset | Look |
|---|---|
| `Tonal` | The default: colour from one seed in tonal palettes, `Tonal` components (pill controls, floating labels, underlined tabs), Inter, soft layered shadows |
| `Quartz` | Crisp and neutral: cool greys and indigo, `Hairline` components with 6 px controls and square chips, semibold Inter headings, small tight shadows, compact |
| `Linen` | Warm and calm: ivory and warm charcoal, terracotta, `Hairline` components with 8 px controls and pill chips, bold Inter headings with Source Serif for display text, a blue focus ring, faint diffuse shadows, compact |

Switch with `theme.WithStyle(preset)`: it keeps the user's light or dark, contrast level, seed and
reduced motion, so a style change doesn't undo their settings.

```csharp
themes.Set(themes.Theme.WithStyle(ThemePresets.Linen), TimeSpan.FromMilliseconds(400));
```

`Theme.Name` says which preset a theme came from (for a picker to tick); `ThemePresets.All` lists
them and `ThemePresets.Find(name)` looks one up. The gallery switches between them live
([components.md](components.md#the-gallery)).

## What a theme holds

| Part | Type | Default |
|---|---|---|
| Colour | `ThemeColors` | Seed `#6750a4`, tonal spot, light, contrast 0, 2021 spec; success, warning and info colours harmonised to the seed; or hand-picked `ColorRoles` for light and dark |
| Shape | `ShapeScale` | Radii 4/8/12/16/20/24/28/32/48 and full, for `CornerShapeRole`; `Control` (buttons and the like) full |
| Type | `TypeScale` | Material 3's 15 styles in Inter plus Destash's extra steps, `Code` in JetBrains Mono, and `Overline` (11 px, spaced, set in capitals by `SurfaceText`) |
| Elevation | `ElevationScale` | Levels 0–5, each any number of `ElevationShadow`s (offset, blur, spread, opacity); by default a key shadow and an ambient shadow |
| Components | `ComponentStyles` | `Tonal`: see [Component styles](#component-styles) |
| State layers | `StateLayerOpacities` | Hover .08, focus .10, pressed .10, dragged .16; disabled .12 container, .38 content |
| Motion | `MotionScheme` | Short/medium/long durations, standard/enter/exit easings, reduced motion |
| Density | `int` | 0 (each step −4 px on controls) |
| Spacing | `SpacingUnit` | 4 px |

## Component styles

Colour, shape and type don't make a look on their own: components differ in size, in which colours
each variant uses, and in how they're built. `Theme.Components` holds that, one record per
family, and components read it instead of constants. Two sets are built in:

| | `ComponentStyles.Tonal` (default) | `ComponentStyles.Hairline` |
|---|---|---|
| Variants | Low-emphasis buttons in the accent; tonal containers | The accent only for the main action and what's on; outlined and text buttons in the ink colour; cards, menus and dialogs on the bright surface (white, or lighter than the page in dark) with 1 px lines |
| Sizes | 40 px buttons padded 24; 48 px menu rows; 56 px drawer rows and fields; 64 px app bar | 40 px buttons padded 14 (36 compact); 36 px menu and drawer rows, inset and rounded; 40 px fields; 56 px app bar with a line under it |
| Tabs, segments | Underlined, icons above labels; joined outlined segments with a tick | A tray with the chosen one a raised pill (sliding, for tabs); icons beside labels |
| Fields | Label floats into the field or its outline | Label above a plain bordered input, ringed in the accent while focused |
| Selection | Halos round indicators; 52 × 32 switch whose handle grows; filled slider thumb | No halos (focus rings the indicator); 16 px boxes and radios; 36 × 20 switch; ringed slider thumb |
| Disabled | Recoloured faint | The usual colours, faded to 50% |
| Overlays | Headline dialogs with text buttons; a dark snackbar bar at the bottom centre | Card dialogs with a close button (and an outlined cancel in an `AlertDialog`); toast cards at the bottom end |
| Lists | 56 px rows in body large; 56 px accordion headers | 44 px rows in body medium, rounded when selected; 48 px accordion headers; rounded calendar days |
| Tracks | Secondary container | The highest surface container (a neutral grey) |
| Chosen items | Secondary container (list rows, table and tree rows, menu bar, master–detail, preferences, wizard steps) | The same, which is a neutral grey in the Hairline presets |
| Showcase | Tinted primary-container hero and call to action, tertiary feature tiles, a raised featured plan, a disc behind an empty state's icon | A quiet outlined hero, a dark (inverse) call to action, accent-filled feature tiles, the featured plan ringed in the accent, an outlined tile behind an empty state's icon |
| Icons | Material Symbols, 24 px, weight 400, chosen ones filled | Outline icons (Lucide), 20 px, never filled |
| Focus ring | 3 px, 2 out, secondary | 2 px, 2 out, primary |
| Hover and press | The content colour laid over the control | A shade (black on light, white on dark), so filled controls darken too; buttons shrink to 98% while pressed |

The records:

- `InteractionStyle`: the focus ring (width, gap, colour family), the `DisabledLook`, the `StateLayerLook` (content colour or shade) and how far buttons shrink when pressed.
- `IconStyle`: the `IconSet` (Material Symbols or outline icons), default size, Material Symbols' weight, and whether chosen icons fill.
- `ButtonStyle`, `IconButtonStyle`: size, padding, corners, label, a `SurfaceLook` per variant, the toggle's on look, and the floating action button's corners and elevation (pagination buttons are 4 px smaller than icon buttons).
- `ChipStyle`: chips' and tags' sizes, corners and looks.
- `CardStyle`: corners, padding and a look per variant.
- `OverlayStyle`: menus, dialogs (and their `DialogLook`), popovers, tooltips, snackbars (`SnackbarLook`) and the scrim.
- `NavigationStyle`: the app bar, the drawer and the `TabsLook`.
- `FieldStyle`: the `FieldLook`, height, corners and text, and calendar days' shape.
- `SelectionStyle`: halos, check box and radio sizes, the `SwitchLook`, the `SliderThumb` (for `Slider` and `RangeSlider`), and tracks' thickness and colour.
- `ListStyle`: list rows' heights, padding, text, corners and selected look (every chosen row and item uses it), and accordion headers.
- `ShowcaseStyle`: the templates' hero, call to action, feature icons, featured plan and empty-state icon.

A `SurfaceLook` is a variant's colours as data: surface and content families, on and container
toggles, outline (its width, and a family to draw it in) and elevation, the same things a
component's facets set. `SurfaceLooks.Surface` and `SurfaceLooks.Pressable` turn one into a
surface, for templates and apps that take their looks from the theme too. Where the structure
differs, an enum picks it and the component builds that structure around the same behaviour, keys
and semantics. Tweak a set with `with`, as `Quartz` does for square chips:

```csharp
Components = ComponentStyles.Hairline with
{
    Chip = ComponentStyles.Hairline.Chip with { Shape = CornerShapeRole.Small },
},
```

Component styles switch at the start of an animated theme change; colours and corners animate.

To restyle part of an app, wrap it in a `ThemeScope`: everything below sees the theme changed by
its function, with the app's colours kept (`ResolvedTheme.Restyled`), so it follows even a theme
transition part way through.

```csharp
new ThemeScope(t => t with { Components = t.Components with { Navigation = t.Components.Navigation with { Tabs = TabsLook.Underline } } }, settingsPage)
```

## Shapes: controls and circles

`CornerShapeRole.Full` is a pill or a circle whatever the theme: avatars, radio buttons, switch
handles, dots. `CornerShapeRole.Control` is for buttons, icon buttons, button and segmented
groups, search fields, pagination and the current navigation item: a pill by default, but a
theme can round them gently instead (`Quartz` 6 px, `Linen` 8 px). In a transition a pill's
corners are mixed from 64 px, so they close steadily rather than all at the end.

## Hand-picked colours

For a theme whose colours aren't worked out from a seed, set `ThemeColors.Light` and
`ThemeColors.Dark` to `ColorRoles`: every role (surfaces, containers, content, outlines, inverse)
and each family as a `ColorFamily` (colour, on, container, on container). The one for the
appearance (`IsDark`) replaces the seed's scheme role for role, so components look the same
either way. The fixed families default to their family's container colours. The seed and
variant don't change a fixed palette, unless `AccentFromSeed` is on: then its primary family
comes from the seed's scheme, so the user's (or the system's) accent colours it while its
neutrals and other colours stay as picked. `WithStyle` carries that choice across presets. A
raised contrast level (following "increase contrast") moves quiet text towards the full text
colour and borders towards stronger ones. The presets' palettes
are tested to keep every family's content at 4.5:1 or more, light and dark.

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
  focused content. The root state is the plain surface (Destash's root was the surface container;
  Material pages sit on the surface and put containers on it). Each role state is a family plus on
  and container flags and an opacity (`Legibility`:
  full 1, high .87, medium .6, low .38, very low .12).
- **Changing it:** a component applies its `SurfaceChange` to the state it inherits
  (`SurfaceState.With`, Destash's `withBackgroundColor`). The change can set a surface family,
  set legibility, toggle on or container, set a content family, or show error or disabled. For
  example, `new SurfaceChange { Surface = SurfaceName.Primary }` makes a filled button: the surface
  is primary, and its content is on-primary.
- **Resolving it:** `ResolvedTheme.Get(role)` turns a role state into a colour, and the opacity
  applies. `ContentColor(state)` and `StateLayerColor(state, opacity)` give *opaque* colours
  instead, mixed into the state's surface in sRGB, which is how Material's opacities are meant to
  look. Components use these (see [components.md](components.md#colours-are-opaque)).

## Runtime

- **Reading the theme:** `ThemeProvider` gives its subtree the controller's current
  `ResolvedTheme` (`context.UseTheme()`) and ticks transitions on the UI's frames.
  `context.UseSurface()` reads the surface state; surface components provide a changed one to their
  children through `ThemeContexts.Surface`.
- **Transitions:** colours mix in OKLab, so blue to orange doesn't pass through grey, and radii
  interpolate. Type, motion and density switch at once, so text doesn't reflow every frame. With
  reduced motion, changes are immediate. Each frame of a transition rebuilds the theme's readers;
  on the heaviest gallery page that's a 0.65 ms median update and 1.8 ms at the 99th percentile
  (Native AOT), measured with `radiant-gallery --snapshot x.png --bench 1200 --bench-theme`.
- **Following the system:** `new ThemeProvider(themes, app) { FollowAppearance = true }` (or
  `themes.FollowAppearance(appearance)`) maps the user's dark mode, accent colour, increased
  contrast and reduced motion onto the theme, before the first frame and whenever they change
  (see [platform.md](platform.md#appearance)).
- **Resolved values:** `ResolvedTheme` computes every role once when resolved, so a lookup is an
  array index.
