# Radiant Testing

`Radiant.Testing` renders UI to images in tests and compares them with goldens: PNGs checked in
beside the tests. It doesn't depend on a test framework. A mismatch throws
`GoldenMismatchException`, and a machine without a GPU gets a null canvas, so each test decides
how to skip.

## Rendering

- **`GpuCanvas.TryCreate(width, height, pixelScale)`** is a headless GPU device with a
  `Renderer2D` for the sRGB format a window gets, sized in logical units. `pixelScale: 2` draws
  at Retina density. It returns null where there's no GPU.
- **`UISnapshot.Render(canvas, element, act: …)`** mounts a UI at the canvas's size and runs
  `act` once it's laid out, to hover, press or focus something through the `UIRoot`. It then
  steps 30 frames on a fixed 60 Hz clock, so transitions finish the same way every run, and
  paints.
- **`Snapshot`** is the result: straight-alpha RGBA in the sRGB encoding the screen shows. It
  offers `PixelAt`, `SavePng`, `LoadPng`, and `Row` and `Column`, which lay separate renders side
  by side on one sheet.

```csharp
using var canvas = GpuCanvas.TryCreate(140, 64, pixelScale: 2f) ?? Skip();
var pressed = UISnapshot.Render(canvas, Themed(new SurfaceButton("Button")), act: root => root.PointerDown(centre));
Golden.AssertMatches(pressed, "Button_pressed");
```

## Goldens

`Golden.AssertMatches(snapshot, name)` compares with `TestData/Golden/<name>.png` in the test
project.

- **Comparison:** pixel by pixel, blended over white, by their distance in YIQ (the measure
  pixelmatch uses). `GoldenOptions.Threshold` (0.1 by default) passes anti-aliasing noise but not
  a changed colour, and `MaxDifferentPixels` (0) is how many may still differ.
- **On a mismatch:** it writes `<name>.actual.png`, `.expected.png` and `.diff.png` (the image
  faded, with the differences in red) to `TestResults/Golden`.
- **New and changed goldens:** a golden that doesn't exist is written from the image, and the
  test passes: look at it before checking it in. After an intended change,
  `UPDATE_GOLDEN_IMAGES=true dotnet test …` rewrites them; review the diffs in git.
- **They're this machine's.** Goldens come from the GPU the tests run on; another GPU can differ
  by a few edge pixels. With no remote CI, that's this Mac.

## What's covered

`Radiant.Components.Tests/ComponentGoldenTests` (category `Gpu`) keeps sheets in light and dark
at 2×:

| Golden | What's on it |
|---|---|
| `Buttons` | every button variant, with an icon, and disabled |
| `ButtonStates` | filled and outlined buttons resting, hovered, focused from the keyboard, and pressed, each rendered by real input |
| `SelectionControls` | check boxes (off, on, indeterminate, error, disabled), radios and switches |
| `Fields` | filled and outlined text fields (label only, with a value and icon, with an error, disabled) and a slider |
| `Display` | chips, badges, a card, an alert and a progress bar |
| `Navigation`, `NavigationRail` | a top app bar, tabs with icons, a breadcrumb, pagination, a segmented button; a rail with a badge |
| `Lists`, `Table` | list items (one and two lines, selected, disabled) and a tree; a sortable data table with checkboxes and a selected row |
| `Feedback` | circular progress, avatars and an accordion with one section open |
| `Menu`, `MenuKeyboard`, `Dialog`, `Tooltip` | an open menu, the same moved through with the arrows (its focus ring inside the item), a dialog over its scrim, and a tooltip after hovering |
| `RightToLeft` | a top app bar, tabs, controls, a text field and a slider laid out right to left |

They're in `ComponentGoldenTests` and `MoreComponentGoldenTests`, with `GoldenSheets` for the
themed surface and the light and dark check.

`Radiant.Tests` has the renderer's own goldens (`Gpu_ReferenceScene`; see
[rendering.md](rendering.md)) and `Radiant.Testing.Tests` checks the harness itself.
