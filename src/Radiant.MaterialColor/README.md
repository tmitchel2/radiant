# Radiant.MaterialColor

A C# port of Google's [material-color-utilities](https://github.com/material-foundation/material-color-utilities)
(Apache-2.0; see `LICENSE` here), the algorithms behind Material 3's colour system: the HCT colour
space, tonal palettes, dynamic schemes and their colour roles, contrast, blending, and extracting a
source colour from an image.

- **Pinned upstream:** `5b3618b16fdc3825e21d5679bafd144662088ea1` (2026-08-21).
- **Ported from the TypeScript sources**, because the fixtures that pin its behaviour come from
  them. The quantizers follow the Java port instead: its k-means is seeded, while TypeScript's uses
  `Math.random`.
- **Colours are `0xAARRGGBB` ints**, as in upstream's Java and Dart ports. Opaque colours are
  negative; `Radiant.Graphics2D.Color.FromArgb(int)` converts them for drawing.
- **Rounding goes through `MathUtils.Round`**, which rounds halves towards positive infinity as
  JavaScript and Java do. .NET's `Math.Round` rounds halves to even.
- **`Hct` is immutable.** Upstream's setters become `WithHue` / `WithChroma` / `WithTone`.
- **Deprecated upstream APIs are not ported:** the legacy `Scheme` and `CorePalette`.

## Verifying against upstream

`tools/mcu-fixtures/generate.sh` checks out the pinned commit, builds its TypeScript (and Java, for
quantization), and records its outputs over a spread of inputs in
`src/Radiant.MaterialColor.Tests/Fixtures`. The tests require every colour to match exactly. Doubles
may differ by up to a relative 1e-9, because V8's `pow` and trigonometric functions can differ from
.NET's in the last bit.

To move to a newer upstream: change the commit in `generate.sh`, regenerate, port the differences
until the tests pass, and update the commit above.
