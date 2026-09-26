# Improvements and known issues

This is a running list of workarounds, shortcuts and rough edges found while building the
platform. Add to it whenever something is done the second-best way, and remove an entry when it
is fixed. The newest entries in each section come first.

Each entry says what's wrong, why it's that way now, and what would be better.

## UI core (`Radiant.UI.Core`)

- **Callbacks make props unequal.** Records compare delegates by reference, so a component
  given a fresh lambda each build always rebuilds. The P5 generator should emit props equality
  that ignores delegates, and refresh callbacks in place.
- **Children lists compare by reference.** A host element whose parent rebuilds always updates,
  even when its children are equal element for element. A structural list comparison, or a
  generated one, would let unchanged subtrees skip.
- **Nothing is idle.** `RadiantUI.Run` renders every frame. `UIRoot.FrameRequested` exists but
  isn't used to idle the loop; add idle waiting, then damage regions (P11).
- **Effect order is approximate.** Effects run deepest first. That runs children before parents,
  but isn't React's strict post-order across sibling subtrees.
- **Text sizes round up to whole pixels.** Rounding up keeps text from wrapping earlier when it's
  laid out at its measured width. 1/64 px would be closer.
- **Styles reset in full.** A changed `LayoutStyle` resets every Yoga property and re-applies it;
  diffing would touch fewer.
- **Yoga isn't CSS by default.** Its default `flexShrink` is 0 (CSS: 1), so scroll areas force
  shrink 1. Consider Yoga's web-defaults config for every node, so layout matches CSS
  expectations everywhere.
- **Percentage minimums inside nested scroll areas.** Yoga.Net resolves them against the outer
  area. `ScrollArea` fills its viewport with `flexGrow` instead. Worth a minimal repro upstream.
- **Duplicated style mapping.** `YogaStyle` repeats the legacy `YogaLayoutEngine` mapping. It
  becomes the only copy when the legacy UI is deleted (the hard replace).
- **Two kinds of key.** `Key` is an element's identity, so keyboard keys are `KeyCode`, whose
  values equal Silk's. `RadiantApplication` and `InputState` still expose Silk's `Key` and
  `MouseButton`; the P7 platform layer should expose only Radiant types.
- **Wheel units are a guess.** GLFW reports notches, converted at `UIAppOptions.WheelStep` (40 px).
  Trackpads send precise deltas that this over-scales. Fix with the P7 platform input.
- **No key repeat or IME.** Input has no key-repeat flag, and text arrives one `char` at a time.
  Fix with P7's `NSTextInputClient`.
- **`ScrollArea` is basic.** It has no thumb dragging, no keyboard scrolling, and no fading
  indicators (`Auto` behaves like `Always`).
- **`ElementRef.Bounds` ignores transforms.** It gives the untransformed rectangle.
- **Semantics have no actions.** Nodes can't be pressed, incremented or scrolled through the
  tree yet, which the P7 accessibility bridge needs.

## Generators (`Radiant.Generators`)

- **Props equality still compares callbacks.** Delegate-ignoring equality isn't generated, because
  skipping a rebuild when only callbacks differ would leave the old callbacks captured in the
  subtree built from the old props. It needs a way to read the latest props (a stable callback
  wrapper or a `UseLatest`) first.
- **No hook-order analyzer yet.** RAD001, for hooks called conditionally, doesn't exist; the
  runtime throws when hook order changes.
- **Forwarders are private.** A derived component can't call them. Records are usually sealed, but
  an option for `protected` may be wanted.
- **Some planned generation is missing.** Snapshot structs, `[Lerpable]`, story knob metadata and
  AOT factories aren't generated yet.
- **Generation runs whole.** The generator collects every candidate record and dedupes by name,
  which re-runs output for every record on any change. Fine at this scale; key the pipeline by
  symbol if large projects feel slow.

## Text (`Radiant.Text`)

- **Layout speed.** 10k characters lay out in ~6 ms (Release); shaping alone is ~2 ms.
  - Placing glyphs searches graphemes per cluster.
  - Splitting into runs allocates per grapheme.
  - Each bidi paragraph copies its text.
- **Tabs are fixed width** (`TabSize` spaces); real tab stops are needed.
- **No justification or hyphenation.**
- **Selecting a line break shows nothing.** Editors usually show a sliver for the newline and for
  empty lines.
- **An empty last line takes the previous paragraph's direction.**
- **Every size is a new font instance.** An `opsz` instance per distinct size means animated
  sizes create many; quantise `opsz`.
- **Word and grapheme `Previous`/`Next` are O(n) per call.** `Paragraph` caches the boundary
  arrays; other callers should too.
- **General_Category is partial.** Its table keeps only the values line breaking needs. Generate
  it in full when another algorithm needs more.
- **One caret case is untested.** Carets inside a right-to-left ligature: no test font merges
  right-to-left clusters.
- **Two HarfBuzzSharp gaps.** It lacks `Font.MakeImmutable` (P/Invoked), and `Blob.FromStream`
  keeps managed memory, which a compacting GC moved (fixed: native copy).

## Rendering (`Radiant.Graphics2D`)

- **Slug is costly and soft when small.** It isn't pixel-snapped, so it's a little softer than
  coverage at small sizes. It also costs 3–4× coverage on the GPU: ~3.7 ms for a full 1080p
  screen of 14 px text on an M4 Pro, against ~0.8 ms.
  - Features under 2 px (a period at 11 px) are its weak spot, because it takes one ray each way
    rather than the pixel's area.
  - The paper's band splitting and corner clipping aren't implemented.
- **Slug's cache is per font instance.** Inter's optical-size axis gives each size from 14 to 32 px
  its own instance, so its own curve data. Share curve data across `opsz`, or quantise.
- **One Slug GPU test depends on a chosen offset** (`-10.37f`). An earlier value put a crossbar edge
  exactly on a pixel boundary; a different font version could trip it again.
- **The Slug patent dedication isn't on Google Patents yet.** It dedicates US 10,373,352 to the
  public domain from 2026-03-17, per Lengyel's post
  (https://terathon.com/blog/decade-slug.html), but Google Patents still shows the patent as
  active. Recheck later.

- **The coverage atlas empties itself.** It clears in full past four pages rather than evicting the
  least-recently-used glyphs. Each glyph is also uploaded on its own; batch the uploads per frame.
- **Text gamma is a heuristic tuned by eye.** It mixes by the text's luminance. Tune it against
  native reference crops at 11–15 px, 1× and 2×; this needs screen capture or a reference
  renderer.
- **Rotated coverage text is soft.** Glyphs under rotation are unsnapped. Use `TextRendering.Hybrid`
  once runtime MSDF lands.
- **Nested transforms cost more.** Transforms apply to vertices on the CPU at `PopTransform`, so
  nesting is O(depth × vertices), and glyph snapping composes the stack per run. A per-draw
  transform (instancing, or a uniform per batch) would fix both.
- **Clipping and layers are limited.**
  - Only the innermost rounded clip applies.
  - Windows have no MSAA.
  - There is no backdrop blur.
  - Opacity layers need the frame begun with its attachment size.

## Assets and licences

- **SixLabors.ImageSharp** (Split License) still decodes PNGs. Consider a permissively licensed
  decoder; ask first.
- **`MsdfBaker` is due for replacement.**
  - It still uses SixLabors.Fonts 2.0.7.
  - Its MSDF core fixes signs with an even-odd test that flips individual channels, which breaks
    corners and overlapping contours.
  - Replace it with the runtime MSDF generator and HarfBuzz outlines once they land.

## Tooling and process

- **The Write tool writes escapes as characters.** It turns `\uXXXX` escapes in C# strings into
  the literal (often invisible) characters. Check new files for control or invisible characters
  and write escapes back.
- **BOMs are inconsistent.** `.editorconfig` asks for UTF-8 with BOM, but newer files have none,
  and some csproj files are CRLF. Settle on one convention and normalise.
- **No window capture.** Agent sessions can't capture windows, so visual checks render offscreen
  (`HeadlessGpu` + `OffscreenReadback`) to PNG.
- **GPU tests run on this Mac only**, by design: there is no remote CI.
