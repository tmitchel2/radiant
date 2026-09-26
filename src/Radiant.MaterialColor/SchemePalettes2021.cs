// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;

namespace Radiant.MaterialColor;

/// <summary>
/// How a scheme's palettes derive from its source color under the 2021 spec (and for the variants
/// later specs don't redefine). Upstream's <c>DynamicSchemePalettesDelegateImpl2021</c>.
/// </summary>
internal class SchemePalettes2021
{
    public virtual TonalPalette GetPrimaryPalette(Variant variant, Hct sourceColorHct, bool isDark, Platform platform, double contrastLevel)
    {
        switch (variant)
        {
            case Variant.Content:
            case Variant.Fidelity:
                return TonalPalette.FromHueAndChroma(
                    sourceColorHct.Hue, sourceColorHct.Chroma);
            case Variant.FruitSalad:
                return TonalPalette.FromHueAndChroma(
                    MathUtils.SanitizeDegreesDouble(sourceColorHct.Hue - 50.0), 48.0);
            case Variant.Monochrome:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 0.0);
            case Variant.Neutral:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 12.0);
            case Variant.Rainbow:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 48.0);
            case Variant.TonalSpot:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 36.0);
            case Variant.Expressive:
                return TonalPalette.FromHueAndChroma(
                    MathUtils.SanitizeDegreesDouble(sourceColorHct.Hue + 240), 40);
            case Variant.Vibrant:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 200.0);
            default:
                throw new ArgumentException($"Unsupported variant: {variant}", nameof(variant));
        }
    }

    public virtual TonalPalette GetSecondaryPalette(Variant variant, Hct sourceColorHct, bool isDark, Platform platform, double contrastLevel)
    {
        switch (variant)
        {
            case Variant.Content:
            case Variant.Fidelity:
                return TonalPalette.FromHueAndChroma(
                    sourceColorHct.Hue,
                    Math.Max(
                        sourceColorHct.Chroma - 32.0, sourceColorHct.Chroma * 0.5));
            case Variant.FruitSalad:
                return TonalPalette.FromHueAndChroma(
                    MathUtils.SanitizeDegreesDouble(sourceColorHct.Hue - 50.0), 36.0);
            case Variant.Monochrome:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 0.0);
            case Variant.Neutral:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 8.0);
            case Variant.Rainbow:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 16.0);
            case Variant.TonalSpot:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 16.0);
            case Variant.Expressive:
                return TonalPalette.FromHueAndChroma(
                    DynamicScheme.GetRotatedHue(
                        sourceColorHct, (double[])[0, 21, 51, 121, 151, 191, 271, 321, 360],
                        (double[])[45, 95, 45, 20, 45, 90, 45, 45, 45]),
                    24.0);
            case Variant.Vibrant:
                return TonalPalette.FromHueAndChroma(
                    DynamicScheme.GetRotatedHue(
                        sourceColorHct, (double[])[0, 41, 61, 101, 131, 181, 251, 301, 360],
                        (double[])[18, 15, 10, 12, 15, 18, 15, 12, 12]),
                    24.0);
            default:
                throw new ArgumentException($"Unsupported variant: {variant}", nameof(variant));
        }
    }

    public virtual TonalPalette GetTertiaryPalette(Variant variant, Hct sourceColorHct, bool isDark, Platform platform, double contrastLevel)
    {
        switch (variant)
        {
            case Variant.Content:
                return TonalPalette.FromHct(DislikeAnalyzer.FixIfDisliked(
                    new TemperatureCache(sourceColorHct).Analogous(count: 3, divisions: 6)[2]));
            case Variant.Fidelity:
                return TonalPalette.FromHct(DislikeAnalyzer.FixIfDisliked(
                    new TemperatureCache(sourceColorHct).Complement));
            case Variant.FruitSalad:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 36.0);
            case Variant.Monochrome:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 0.0);
            case Variant.Neutral:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 16.0);
            case Variant.Rainbow:
            case Variant.TonalSpot:
                return TonalPalette.FromHueAndChroma(
                    MathUtils.SanitizeDegreesDouble(sourceColorHct.Hue + 60.0), 24.0);
            case Variant.Expressive:
                return TonalPalette.FromHueAndChroma(
                    DynamicScheme.GetRotatedHue(
                        sourceColorHct, (double[])[0, 21, 51, 121, 151, 191, 271, 321, 360],
                        (double[])[120, 120, 20, 45, 20, 15, 20, 120, 120]),
                    32.0);
            case Variant.Vibrant:
                return TonalPalette.FromHueAndChroma(
                    DynamicScheme.GetRotatedHue(
                        sourceColorHct, (double[])[0, 41, 61, 101, 131, 181, 251, 301, 360],
                        (double[])[35, 30, 20, 25, 30, 35, 30, 25, 25]),
                    32.0);
            default:
                throw new ArgumentException($"Unsupported variant: {variant}", nameof(variant));
        }
    }

    public virtual TonalPalette GetNeutralPalette(Variant variant, Hct sourceColorHct, bool isDark, Platform platform, double contrastLevel)
    {
        switch (variant)
        {
            case Variant.Content:
            case Variant.Fidelity:
                return TonalPalette.FromHueAndChroma(
                    sourceColorHct.Hue, sourceColorHct.Chroma / 8.0);
            case Variant.FruitSalad:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 10.0);
            case Variant.Monochrome:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 0.0);
            case Variant.Neutral:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 2.0);
            case Variant.Rainbow:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 0.0);
            case Variant.TonalSpot:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 6.0);
            case Variant.Expressive:
                return TonalPalette.FromHueAndChroma(
                    MathUtils.SanitizeDegreesDouble(sourceColorHct.Hue + 15), 8);
            case Variant.Vibrant:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 10);
            default:
                throw new ArgumentException($"Unsupported variant: {variant}", nameof(variant));
        }
    }

    public virtual TonalPalette GetNeutralVariantPalette(Variant variant, Hct sourceColorHct, bool isDark, Platform platform, double contrastLevel)
    {
        switch (variant)
        {
            case Variant.Content:
                return TonalPalette.FromHueAndChroma(
                    sourceColorHct.Hue, (sourceColorHct.Chroma / 8.0) + 4.0);
            case Variant.Fidelity:
                return TonalPalette.FromHueAndChroma(
                    sourceColorHct.Hue, (sourceColorHct.Chroma / 8.0) + 4.0);
            case Variant.FruitSalad:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 16.0);
            case Variant.Monochrome:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 0.0);
            case Variant.Neutral:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 2.0);
            case Variant.Rainbow:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 0.0);
            case Variant.TonalSpot:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 8.0);
            case Variant.Expressive:
                return TonalPalette.FromHueAndChroma(
                    MathUtils.SanitizeDegreesDouble(sourceColorHct.Hue + 15), 12);
            case Variant.Vibrant:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 12);
            default:
                throw new ArgumentException($"Unsupported variant: {variant}", nameof(variant));
        }
    }

    public virtual TonalPalette? GetErrorPalette(Variant variant, Hct sourceColorHct, bool isDark, Platform platform, double contrastLevel)
    {
        return null;
    }
}
