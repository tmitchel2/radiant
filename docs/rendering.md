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
- Slug text is worked out from the glyph outlines per pixel, so it is exact at any scale or angle.

Clips are not transformed; they are always in window coordinates.

## Clipping

`PushClip(x, y, w, h)` clips to a rectangle with the GPU scissor. `PushClip(x, y, w, h, radii)`
also cuts to rounded corners with a one-pixel anti-aliased edge, so a rounded panel's content
stops at its corners. Clips are in logical window coordinates and nest by intersection. Only the
innermost rounded clip's corners are applied: a rounded clip inside another rounded clip does not
also get the outer one's corners.

How rounded clipping works:
- The clip is not in the vertices. The uniforms are an array of 256-byte slots, one per distinct
  clip in the frame (slot 0 is "no rounded clip"), each holding the projection and one clip in
  device pixels.
- A batch selects its slot with a dynamic offset.
- Every fragment shader multiplies its output by the clip's coverage.

So rounded clipping costs no vertex data and no extra pipeline, and batches that share a clip
still merge.

## Opacity layers

`PushLayer(opacity)` / `PopLayer()` fade a group as one image: where two of its shapes overlap, the
lower one does not show through the upper. Layers nest, their opacities multiply, and clips and
transforms inside them work as usual.

Each layer's content is rendered into a pooled offscreen texture the size of the attachment
(multisampled and resolved, when the renderer is). Inner layers are rendered first. Then a batch
in the parent, at the point the layer was pushed, composites the texture at the layer's opacity.
The layer passes are encoded on the renderer's own command buffer and submitted inside `EndFrame`,
before the caller submits the main pass, so `EndFrame(RenderPassEncoder*)` is unchanged.

Layers need the attachment size, so they need the clip-aware `BeginFrame(width, height, scale)`.
Each costs a full-size texture and pass, so keep them for groups that actually overlap. A lone
shape can simply use a translucent color.

## Shadows

`DrawShadow(x, y, w, h, radii, blur, color, offset, spread)` draws a soft shadow of a rounded
rectangle, as CSS `box-shadow` does: grown by `spread`, moved by `offset`, blurred by `blur` (the
Gaussian's standard deviation is half the blur, as in CSS). Draw it before the surface that casts it;
an elevation is typically two shadows, a tight key light and a soft ambient one.

It is a shape kind in the SDF pipeline, computed analytically: the blur is separable, so it is
integrated in closed form along x and with four samples along y. This is Evan Wallace's "Fast Rounded
Rectangle Shadows". With square corners the result is within 4/255 of the exact Gaussian blur,
which a GPU test checks pixel by pixel.

## Gradients

`DrawRoundedRectFilled`, `DrawRoundedRect` and `DrawDisc` take a `Gradient` in place of a fill color.
A gradient is linear (`Gradient.Linear(start, end, …)`) or radial (`Gradient.Radial(center, radius,
…)`), with two to four stops; before the first stop and after the last, the end colors hold. Its
points are in draw coordinates, and the gradient turns and scales with its shape.

`GradientInterpolation` chooses the space the stops blend in:
- `Srgb` (the default) is what CSS and design tools do, so a design matches its mock-up.
- `Linear` mixes light physically.
- `Oklab` gives perceptually even steps.

Alpha always blends premultiplied, so a fade from transparent keeps its hue. The stops travel in the
SDF shape's vertices, so a gradient needs no texture and batches with plain shapes.

## Text

Shaping, Unicode and paragraph layout live in `Radiant.Text`; see [text.md](text.md). The
renderer draws laid-out text as `TextRendering` says (a coverage atlas by default, distance
fields generated at runtime, a hybrid of the two, or Slug), and strings from fonts baked offline.

### Coverage text (laid-out paragraphs)

`DrawParagraph(paragraph, position)` and `DrawGlyphRun(run, offset)` draw text laid out by
`Radiant.Text` from a **coverage atlas** (`GlyphAtlas`).

- **Rasterizing:** each glyph is rasterized on the CPU (`GlyphRasterizer`, exact-area coverage) at
  the size it appears on screen, which is its size × the pixel scale × the transform's scale.
- **Pixel grid:** the baseline snaps to a whole device pixel and the pen to a quarter, so small
  text is as sharp as the grid allows. Glyphs are cached per quarter-pixel position.
- **Pages:** 1024² single-channel textures packed in shelves, each glyph with a blank border.
  Past four pages the atlas is emptied at the next frame.
- **Weight:** edge coverage is corrected for `TextGamma` (default 1.8) by the text's luminance, so
  dark text on light keeps the weight it was designed with rather than looking thin in linear-light
  blending. Set it to 1 to blend coverage as it is.
- **Transforms:** under a move-and-scale transform, glyphs snap. Under a rotation they're drawn
  unsnapped and softer; turning or zooming text is what MSDF (or `Hybrid`) is for.

It is a sixth batch kind (`Coverage`), so it keeps its place in draw order like everything else.

### Slug text

With `TextRendering = TextRendering.Slug`, `DrawParagraph` and `DrawGlyphRun` draw each glyph from
its outline, using Eric Lengyel's Slug method. The patent is dedicated to the public domain, and
the reference shaders are MIT; see `src/Radiant.Text/THIRD-PARTY-NOTICES.md`.

- **Evaluating:** each glyph is one quad. For every pixel, the fragment shader casts one ray right
  and one up through the glyph's curves. It sums the fraction of the pixel before each crossing
  that counts, deciding which count from the signs of the control points alone. That gives
  anti-aliased coverage along both axes, blended by how close each ray's crossings are.
- **Fill rule:** non-zero, so the overlapping contours of variable fonts fill without seams.
- **Data:** glyphs are prepared once per font instance and glyph, whatever the size, by
  `Radiant.Text.Slug`. They are kept in two storage buffers (`SlugGlyphCache`) that the fragment
  shader reads. Only newly prepared glyphs are uploaded at the end of a frame. Past 32 MB the cache
  is emptied at the next frame.
- **Dilation:** each quad is grown so every pixel with any coverage is shaded. On screen, each
  edge moves out by as far as a pixel square reaches across it: half a pixel when upright, up to
  0.71 at 45°. Transforms are affine and known at draw time, so this is a closed form. It is also
  exactly where the shader's anti-aliasing ends.
- **Transforms:** quads are made in local coordinates and moved at `PopTransform`, with their em
  coordinates unchanged. Scaled, rotated and zoomed text is therefore exact, and an 8× zoom keeps
  edges one pixel wide.
- **Colour:** the tint is straight alpha and premultiplied in the shader. Edges get the same
  `TextGamma` correction as coverage text, so the two modes have the same weight.
- **Batching:** it is a seventh batch kind (`Slug`).
- **Accuracy:** a GPU test checks that the shader matches `SlugCoverage`, the CPU reference, to
  within one 255th on every pixel of a turned and scaled glyph.
- **Cost** (Apple M4 Pro, 1920 × 1080, Release):

  | Text | Coverage | Slug |
  |---|---|---|
  | 6,000 glyphs at 14 px | ~0.45 ms | ~1.4 ms |
  | A full screen at 14 px (18,000 glyphs) | ~0.8 ms | ~3.7 ms |
  | 1,800 glyphs at 32 px | ~0.1 ms | ~0.9 ms |

  These are GPU and upload times. Building the quads on the CPU takes about 0.2 µs a glyph in
  either mode. Coverage is cheaper per pixel and sharper at small sizes, since it snaps to the
  pixel grid. Slug has no bitmaps to make or store, and stays exact when text turns or zooms.

### Runtime MSDF text and the hybrid (laid-out paragraphs)

`TextRendering` chooses how `DrawParagraph` and `DrawGlyphRun` draw: `Coverage` (the default),
`Msdf`, `Hybrid`, or `Slug`.

- **`Msdf`:** each glyph's multi-channel distance field is generated once, at runtime, by
  `Radiant.Text`'s port of msdfgen (see [text.md](text.md)), into a **runtime MSDF atlas**
  (`MsdfGlyphAtlas`). It is keyed by font instance and glyph only, generated at a 40 px em with a
  6 px range, and scaled to every size and angle the glyph is drawn at.
  - **Drawing:** quads go in the run's own coordinates, unsnapped, through the MSDF pipeline
    (`BatchKind.Msdf`), so they follow the transform stack like any other draw. The shader works
    out the edge's sharpness per pixel from the range in texels (the page's parameters uniform), so
    edges stay a pixel wide at any scale or rotation.
  - **Pages:** 1024² RGBA textures packed in shelves, with a bind group each. Past four pages the
    atlas is emptied at the next frame.
  - **Generating:** about a millisecond a glyph. A run's new glyphs are generated together, on
    several threads: Inter's letters, digits and a few symbols (66 glyphs) take about 20 ms on
    a 12-core Mac, against 70 ms on one thread.
  - **Colour:** the tint is a straight-alpha linear colour, as for coverage text. There is no
    gamma correction of edges: at the sizes MSDF is for, edges are a small part of the ink.
- **`Hybrid`:** coverage for text at most `HybridThreshold` (24) device pixels and upright (moved
  and scaled only), where pixel-grid snapping makes it sharpest; MSDF for larger text and for text
  under a rotation, where one field serves every size and angle. The size counts the pixel scale
  and the transform's scale.

At 64 px, `Msdf` draws Inter within about 2% of the coverage atlas's ink; turned, its stems stay
as solid.

### Baked MSDF text (`DrawText`)

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

`ReferenceSceneGpuTests` draws one scene using every feature (shadows, a gradient, a rounded clip
over scrolled text, a translucent layer, a rotated bordered card) and compares it with
`TestData/Golden/Gpu_ReferenceScene.png`. It catches changes no targeted test looks for. After an
intended change, regenerate with `UPDATE_GOLDEN_IMAGES=true` and review the image.

The older golden-image tests (`Graphics2D/Visual`) rasterize the filled and line vertex streams on
the CPU. They cannot see text, SDF shapes or images.

## Deferred

| Item | Why deferred | Trigger to revisit |
|---|---|---|
| Nested rounded clips | Only the innermost rounded clip's corners apply | A rounded container inside another visibly overflows the outer corners; clip through a layer mask |
| Multisampling in windows | `RadiantApplication` renders single-sampled; SDF shapes and text anti-alias analytically, but tessellated circles, polygons and thick lines are aliased | A UI draws tessellated geometry prominently |
| Backdrop blur | Needs the scene behind a layer as a texture, blurred | A frosted-glass design |
| Shaped, wrapped, variable-font text in `DrawText` | Its atlases are fixed at bake time and it walks codepoints; laid-out text (`DrawParagraph`) has all of this, from runtime atlases | Moving `DrawText`'s callers to `Paragraph` |
| Material Symbols icons | Nothing draws icons yet, and the full variable icon font is several megabytes to embed for no user | The first icon-bearing components |
| Premultiplying decoded images | Nothing in Radiant decodes images into a `Texture2D` yet | An image-loading API |
