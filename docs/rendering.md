# Radiant Rendering: colour, alpha and text

The rules every draw call relies on. They hold in a window, headless, and in a composited tab frame.

## Colour is linear light

`Renderer2D` draws in **linear light**. The render target is an `*Srgb` format, so the GPU encodes
on write and blends between linear values. This is why 50% white over black comes out as sRGB 188
(half the light) rather than the too-dark 128 you get from blending encoded values.

Encoded colours are decoded exactly once, where they enter the renderer:

| Source | Use |
|---|---|
| `#rgb`, `#rgba`, `#rrggbb`, `#rrggbbaa` (CSS order, alpha last) | `Color.Parse` / `Color.TryParse` |
| `0xAARRGGBB` ints, e.g. from Radiant.ColorSystem (signed ints included) | `Color.FromArgb` |
| 8-bit or 0–1 sRGB components | `Color.FromSrgb8` / `Color.FromSrgb` |
| The Tailwind palette | `Colors.*` (already linear, `Vector3`) |

`Radiant.Graphics2D.Color` is a linear, **straight-alpha** RGBA float struct:
- `WithAlpha` fades a colour without changing its hue.
- `Lerp` interpolates with premultiplied alpha, so a fade to `Transparent` doesn't darken halfway.
- `ToArgb` and `ToString` (`#rrggbbaa`) encode it back.
- It converts implicitly to `Vector4`, so it can go anywhere the renderer takes a colour.

`SrgbTransfer` holds the exact IEC 61966-2-1 curve. Its byte decode is a lookup table and round-trips
all 256 values.

### One format choice

`SurfaceFormats.ChooseColorFormat` makes the format choice for both the window's swapchain
(`RadiantApplication`) and every pipeline (`Renderer2D.Initialize`). It prefers `Bgra8UnormSrgb`,
then `Rgba8UnormSrgb`, and only falls back to the surface's first format if neither exists; in that
case it logs a warning, because colours would come out too dark. The two callers can't disagree
because they call the same function on the same capability list.

`HeadlessGpu` reports a single format (sRGB by default). A headless frame is therefore rendered
exactly as a window would render it.

`SurfaceFormats.ChooseAlphaMode` picks the composite alpha mode:
- **Opaque windows** ask for `Opaque`.
- **Transparent windows** (`RadiantWindowStyle.Transparent`) ask for `Premultiplied`, or failing that
  `Unpremultiplied`. On Metal, wgpu reports only `Opaque` and `Unpremultiplied`, but Core Animation
  composites layer contents as premultiplied either way.

## Alpha is premultiplied on the GPU

Every pipeline blends `One, OneMinusSrcAlpha` for both colour and alpha, and every shader returns RGB
already multiplied by alpha. Callers still pass straight-alpha colours; premultiplying is the
shader's job. Three things depend on this:

- **Coverage accumulates.** Two 50% layers over a transparent target cover 75%. That is what a
  transparent window, or an offscreen frame composited later, needs.
- **Mixing inside a shader stays right.** An SDF border mixed over a transparent fill fades into the
  background. In straight alpha it was pulled towards the fill's invisible RGB.
- **Texel filtering stays right at transparent edges.**

Clear colours are straight alpha too, and are premultiplied before the pass (`ClearColor`).

**`Texture2D` holds premultiplied texels.** A frame rendered by `Renderer2D` already is, which is why
a tab frame can be uploaded and drawn 1:1 without a single channel changing. Image data decoded from
a file (PNG etc.) must be premultiplied before `Update`. `DrawImage`'s tint is a straight-alpha
colour.

## Draws happen in the order they are made

Each primitive kind has its own vertex list and pipeline: filled triangles, hairlines, SDF shapes,
images and MSDF text. The order draws are made in is kept separately, as a list of batches. A batch
is a run of one kind's vertices sharing a clip and a texture. `EndFrame` replays the batches in
order, switching pipeline where the kind changes, so a later draw always covers an earlier one
whatever pipelines they use. Consecutive compatible draws extend the current batch, so a frame of
many rectangles is still a single draw call.

Both `BeginFrame` overloads draw every kind. The parameterless one simply sets no scissor.

## Transforms

`PushTransform(Matrix3x2)` / `PopTransform()` transform everything drawn between them. Nested
transforms apply inner first, as in a scene graph. `PushScrollOffset` is a translation on the same
stack.

Every kind of draw follows a transform exactly:
- SDF shapes move their quad but are evaluated in their own frame, so a rotated rounded rectangle
  stays exact, and anti-aliasing (from screen-space derivatives) stays one pixel wide at any scale.
- MSDF text works out its edge sharpness per pixel, so rotated or scaled text stays crisp.

Clip rectangles are not transformed; they are always in window coordinates. A clip that has to
rotate with its content needs a rounded or path clip, which is not implemented yet.

## Shadows

`DrawShadow(x, y, w, h, radii, blur, color, offset, spread)` draws a soft shadow of a rounded
rectangle, as CSS `box-shadow` does: grown by `spread`, moved by `offset`, blurred by `blur` (the
Gaussian's standard deviation is half the blur, as in CSS). Draw it before the surface that casts it;
an elevation is typically two shadows, a tight key light and a soft ambient one.

It is a shape kind in the SDF pipeline, computed analytically: the blur is separable, so it is
integrated in closed form along x and with four samples along y. This is Evan Wallace's "Fast Rounded
Rectangle Shadows". With square corners the result is within 4/255 of the exact Gaussian blur,
which a GPU test checks pixel by pixel.

## Text

`MsdfFont` atlases are baked offline by `src/MsdfBaker` and embedded in the Radiant assembly.
`EmbeddedFonts` names them:

| Name | Face | Use |
|---|---|---|
| `EmbeddedFonts.Default` (`inter-regular`) | Inter 400 | UI text |
| `InterMedium`, `InterSemiBold` | Inter 500 / 600 | labels, titles, headings |
| `Monospace` (`jetbrains-mono-regular`) | JetBrains Mono 400 | code, figures that must line up |
| `Drafting`, `DraftingMath`, `DraftingShapes` | Noto Symbols / Math / Symbols 2 | GD&T frame symbols (`MsdfFontChain`) |

Loading details:
- `LoadEmbedded("default")` and `LoadEmbedded("monospace")` still resolve.
- `DrawText` registers a font with the renderer on first use, so calling `RegisterMsdfFont` is
  optional.

All fonts are SIL Open Font License. The sources and licences live in `src/Radiant/Assets/Fonts`.

### Rebaking

Run `tools/bake-fonts.sh`. Every atlas is baked at a 40 px em with a 6 px distance range into a
1024² atlas. The bake:
- **Kerning** — measures kerning by laying out every glyph pair with and without it (`KerningExtractor`).
  Inter kerns through GPOS, not a `kern` table.
- **Symbol fallbacks** — fills codepoints the face lacks from Noto fallbacks (`--fallback`), so
  annotation text such as `S⌀ 5.00 mm` draws in one font. The fallbacks are downloaded from a pinned
  google/fonts commit, not committed.
- **Baseline** — makes every glyph's `BearingY` baseline-relative from the glyph's own bounding box.
  That makes it correct for fallback fonts whose vertical metrics differ from the primary's.

The manifest records the distance range, and the MSDF shader reads it per font (a uniform in the
atlas bind group). Edge sharpness therefore stays correct whatever range an atlas was baked with.

## Testing what the GPU draws

`HeadlessGpu` and `OffscreenReadback` (in `Radiant.Graphics2D`) render real frames without a window.
Tests in the `Gpu` category use them through `Radiant.Tests/Graphics2D/GpuFrame.cs` and assert on the
pixels read back:
- sRGB encoding (a hex fill reads back as that hex)
- linear blending
- premultiplied coverage and clears
- clean SDF stroke edges
- anti-aliased MSDF text
- a composited frame being byte-identical (±1) to its source

On a machine with no GPU adapter these tests are reported inconclusive rather than failed.

The older golden-image tests (`Graphics2D/Visual`) rasterize the filled and line vertex streams on
the CPU. They cannot see text, SDF shapes or images.

## Deferred

| Item | Why deferred | Trigger to revisit |
|---|---|---|
| Runtime glyph generation, shaping, wrapping, variable fonts | The atlases are fixed at bake time and `DrawText` walks codepoints | The text stack (`ITextShaper`, paragraph layout, atlas / MSDF / Slug renderers) |
| Material Symbols icons | Nothing draws icons yet, and the full variable icon font is several megabytes to embed for no user | The first icon-bearing components |
| Premultiplying decoded images | Nothing in Radiant decodes images into a `Texture2D` yet | An image-loading API |
