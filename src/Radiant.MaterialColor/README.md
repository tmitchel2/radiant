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
- **Deprecated upstream APIs are not ported:** the legacy `Scheme` and `CorePalette`, and
  `MaterialDynamicColors`' static fields.

## What is here

| Upstream module | Port |
|---|---|
| hct, utils | `Hct`, `Cam16`, `ViewingConditions`, `HctSolver`, `ColorUtils`, `MathUtils` |
| palettes | `TonalPalette` (and the internal `KeyColor` search) |
| contrast, dislike, blend, temperature | `Contrast`, `DislikeAnalyzer`, `Blend`, `TemperatureCache` |
| quantize, score | `QuantizerCelebi`, `QuantizerWu`, `QuantizerWsmeans`, `QuantizerMap`, `PointProviderLab`, `Score` |
| dynamiccolor | `DynamicColor`, `DynamicScheme`, `MaterialDynamicColors`, `ContrastCurve`, `ToneDeltaPair`, `Variant`, `SpecVersion`, `Platform`, and the internal color specs 2021/2025/2026 |
| scheme | `SchemeTonalSpot`, `SchemeContent`, `SchemeFidelity`, `SchemeVibrant`, `SchemeExpressive`, `SchemeNeutral`, `SchemeMonochrome`, `SchemeRainbow`, `SchemeFruitSalad`, `SchemeCmf` |

```csharp
var scheme = new SchemeTonalSpot(Hct.FromInt(unchecked((int)0xFF6750A4)), isDark: true, contrastLevel: 0.0,
    SpecVersion.Spec2025);
int primary = scheme.Primary;                                          // a role, as ARGB
int any = scheme.GetArgb(MaterialDynamicColors.SurfaceContainerHigh()); // or any dynamic color
```

## Where the C# API differs

- `Score.score` is `Score.Rank`: C# cannot name a method after its class.
- `MaterialDynamicColors` is a static class. Upstream's instance methods build a new
  `DynamicColor` on every call; here each role is built once and shared, since colors are immutable
  functions of the scheme. A scheme caches what it resolves, weakly keyed by color.
- Upstream clones colors and schemes with `Object.assign`. Here that is `DynamicColor.With(name,
  tone)`, and, internally, `DynamicScheme.CloneWith(isDark, contrastLevel)`, which keeps the original
  palettes as upstream's copy does.
- String unions are enums: `TonePolarity`, `DeltaConstraint`, `Platform`, `SpecVersion`.
- `TemperatureCache.RelativeTemperature` throws for a color that is not one of the cache's own,
  where upstream returns NaN. Upstream only calls it with its own.
- The quantizers follow the Java port where it and TypeScript differ (all noted in the code):
  - Wsmeans uses a seeded `java.util.Random` (reproduced exactly in `JavaRandom`) and stable-sorts
    its distance rows.
  - Wu uses wrapping integer arithmetic and returns each color once.
  - `QuantizerMap` counts translucent pixels.
  - Wsmeans refuses an empty list of starting clusters, where Java crashes. Celebi always supplies
    one.

## Verifying against upstream

`tools/mcu-fixtures/generate.sh` checks out the pinned commit, builds its TypeScript (and Java, for
quantization), and records its outputs over a spread of inputs in
`src/Radiant.MaterialColor.Tests/Fixtures`. The tests require every colour to match exactly. Doubles
may differ by up to a relative 1e-9, because V8's `pow` and trigonometric functions can differ from
.NET's in the last bit.

The tests also carry upstream's own unit tests, and the guarantee that text reaches a 4.5 contrast
ratio (3.0 at reduced contrast) on its surface in every scheme.

To move to a newer upstream:
1. Change the commit in `generate.sh` and regenerate the fixtures.
2. Re-translate the color specs with `tools/mcu-fixtures/transpile-color-specs.py`, then
   `dotnet format whitespace`.
3. Port the other differences by hand until the tests pass.
4. Update the commit above.
