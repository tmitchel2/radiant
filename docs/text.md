# Radiant Text: fonts, shaping, Unicode and paragraph layout

`Radiant.Text` turns styled text into positioned glyphs, and answers the questions a text view and
an editor ask about them. It also rasterizes glyphs to coverage bitmaps (`GlyphRasterizer`), but
it doesn't touch the GPU: `Renderer2D.DrawParagraph` draws a paragraph from a coverage atlas (see
[rendering.md](rendering.md)).

```
AttributedText ─┐
ParagraphStyle ─┼─► Paragraph.Layout ─► Paragraph ─► Lines ─► GlyphRuns (font, glyphs, advances, origin)
FontLibrary ────┘                            │
                                             └─► HitTest · GetCaretRect · GetSelectionRects · MoveCaret · GetWordRange
```

## Fonts

- **Embedded fonts:** `FontLibrary.Default` holds Inter and JetBrains Mono. Both are variable
  fonts, both have italics, and both are embedded in the assembly (SIL Open Font License).
- **Families:** `FontLibrary.Resolve(family, weight, italic, size)` returns a `FontInstance`. It sets
  `wght` to the weight and `opsz` to the size, so small text gets the design Inter tunes for small
  sizes. Instances are cached per face and axis values.
- **Fallback:** faces registered with `fallback: true` are tried in order for characters the
  requested family lacks. Layout chooses a font per grapheme, by the grapheme's first character,
  so a combining mark stays in its base's font.
- **Font data:** HarfBuzz reads a font in place, so `FontFace` gives it a native copy that it
  frees itself. A managed array could be moved by a compacting garbage collection, after which
  every glyph would shape as `.notdef`.

## Shaping

`TextShaper` shapes one run with HarfBuzz (MIT) in one font and one direction. That covers
ligatures, kerning, mark placement, Arabic joining and Indic reordering.

Results:
- Positions are in pixels with y down.
- Glyphs come out in visual order.
- `Clusters` index into the whole text.

`ShapeOptions` carries direction, script, language, OpenType features, tracking and tab size.

Control characters draw nothing and take no space. A tab advances by `TabSize` spaces; real tab
stops are not done yet.

## Unicode

All the tables are generated from the Unicode 16.0.0 Character Database by
`tools/unicode-data/generate.sh`. Each algorithm passes its official conformance file in full:

| Algorithm | API (`Radiant.Text.Unicode`) | Conformance |
|---|---|---|
| UAX #14 line breaking | `LineBreaker.GetOpportunities` | LineBreakTest: 16,672 / 16,672 |
| UAX #9 bidi | `BidiParagraph.Resolve`, `GetLineLevels`, `GetVisualOrder`, `ReorderLevels` | BidiTest: 770,241 / 770,241; BidiCharacterTest: 91,707 / 91,707 |
| UAX #29 graphemes | `GraphemeBoundaries` | GraphemeBreakTest: 1,093 / 1,093 |
| UAX #29 words | `WordBoundaries` (and `IsWord` to skip spaces and punctuation) | WordBreakTest: 1,826 / 1,826 |

.NET's own `StringInfo` gets 7 of the grapheme cases wrong: it splits Indic conjuncts under rule GB9c.
That is why graphemes are implemented here.

## Paragraph layout

`Paragraph.Layout(AttributedText, ParagraphStyle, FontLibrary)` works in two stages.
`ParagraphBuilder` does the first stage, the work that doesn't depend on width, once:

1. **Bidi:** the text is split into bidi paragraphs at hard line breaks. Each finds its own direction,
   as Unicode says, so a Hebrew line after an English one reads right to left. `ParagraphStyle.Direction` forces one.
2. **Runs:** text is split wherever the style, font, script or bidi level changes. Characters with no
   script of their own (spaces, digits, punctuation) join the run they're in, and stay in its font
   when that font has them.
3. **Shaping:** each run is shaped whole, and each character's advance is recorded as a prefix sum.

The second stage breaks lines at a width and places glyphs. `Paragraph.WithMaxWidth` repeats only
this stage, which is what a layout engine's measure callback needs.

**Line breaking** is greedy over UAX #14 opportunities.
- **Trailing spaces hang:** they don't count towards whether a line fits, or where it aligns.
- **Long words:** a word too long for a line of its own breaks between graphemes (`BreakLongWords`),
  or else overflows.
- **Trailing line break:** text that ends with a line break gets an empty last line, for the caret.

**Max lines:** `MaxLines` keeps that many lines. The last one takes the rest of its paragraph, cut
between graphemes to leave room for the `Ellipsis`, so it fills the width. The ellipsis sits at
the line's logical end.

**Line pieces:** each line is cut into pieces by run and by line-level bidi level (rule L1 puts
trailing spaces at the paragraph's level).
- A piece whose cut HarfBuzz marks safe reuses the run's glyphs; the rest are reshaped, seeing only
  the line's text, so an Arabic letter before a break takes its final form.
- Pieces are ordered on screen by rule L2.

**Vertical metrics** follow CSS: a line box is as tall as its tallest text. A set `LineHeight`, or
else the font's line gap, is split half above and half below.

**Alignment:**
- `Start` and `End` follow each line's direction.
- Trailing spaces hang past the end of the line, which is its left in right-to-left text.
- An unwrapped paragraph aligns within its longest line.

**Sizes:** `MinIntrinsicWidth` (the widest word) and `MaxIntrinsicWidth` (the widest line between
line breaks) are for layout engines.

## Carets, hit testing and selection

A laid-out line keeps a box per grapheme, left to right on screen. A ligature that stands for
several graphemes, such as Inter's `->` arrow, is shared out evenly among them, so the caret can go
between them.

- **Positions:** a `TextPosition` is an index plus a `TextAffinity`. At a wrap the same index ends
  one line (`Upstream`) and starts the next (`Downstream`). At a change of direction, affinity
  chooses which neighbour the caret stands beside.
- **Hit testing:** `HitTest` places the caret on the nearer side of the grapheme under the point.
  Beside a line, it goes to the line's visual end; below the text, to the last line.
- **Selection:** `GetSelectionRects` gives one box per line and per direction run, so a selection
  that crosses from Latin into Hebrew draws as two boxes.
- **Caret movement:** `MoveCaret` supports:
  - arrow keys, by grapheme on screen: in right-to-left text, Left moves forward
  - by grapheme in reading order
  - by word: forward to word ends, backward to word starts, skipping punctuation and spaces
  - Up and Down, with a goal x that keeps the column across short lines
  - line start and end, where line end stops before the line break, or upstream of a wrap
  - document start and end

## Rasterizing

`GlyphRasterizer.Rasterize(outline, scale, offset)` gives each pixel the exact fraction of its area
inside the outline. It uses the signed-area accumulation of font-rs (see `THIRD-PARTY-NOTICES.md`):
- **Curves** are flattened to within 0.05 px.
- **Overlapping contours** of variable fonts fill as non-zero.
- **Positioning:** `offset` places the pen within its pixel, which is how the atlas makes its
  quarter-pixel positions.

## Performance

Measured in Release on 10,000 characters of wrapped Latin text, 206 lines:

| Operation | Time |
|---|---|
| `TextShaper` shaping the whole text in one run | ~1.8 ms |
| `Paragraph.Layout` (bidi, segmentation, shaping, breaking, placing) | ~6 ms |
| `WithMaxWidth` to lay it out again at a new width | ~0.7 ms |
| `HitTest` | ~1 µs |
| arrow-key `MoveCaret` | ~10 µs |

## Not yet

- **More ways to draw glyph runs:** MSDF generated at runtime (for large, rotating or zooming
  text, and the hybrid of coverage below a size and MSDF above it), and Slug.
  `Renderer2D.DrawText` still draws from the baked MSDF atlases.
- **Line layout:**
  - tab stops, with tabs positioned by where they fall on the line
  - justification
  - hyphenation
- **Fonts:** colour emoji (COLR/sbix) and system font fallback.
- **Tests:** caret splitting of a right-to-left ligature. No test font merges right-to-left
  clusters, so only left-to-right ligatures are tested.
