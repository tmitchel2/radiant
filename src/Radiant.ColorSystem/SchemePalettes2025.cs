// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

namespace Radiant.ColorSystem;

/// <summary>
/// How a scheme's palettes derive from its source color under the 2025 spec: platform-aware chroma,
/// hue rotations per hue range, and an error palette of its own. Upstream's
/// <c>DynamicSchemePalettesDelegateImpl2025</c>.
/// </summary>
internal sealed class SchemePalettes2025 : SchemePalettes2021
{
    public override TonalPalette GetPrimaryPalette(Variant variant, Hct sourceColorHct, bool isDark, Platform platform, double contrastLevel)
    {
        switch (variant)
        {
            case Variant.Neutral:
                return TonalPalette.FromHueAndChroma(
                    sourceColorHct.Hue,
                    platform == Platform.Phone ? (Hct.IsBlue(sourceColorHct.Hue) ? 12 : 8) :
                                           (Hct.IsBlue(sourceColorHct.Hue) ? 16 : 12));
            case Variant.TonalSpot:
                return TonalPalette.FromHueAndChroma(
                    sourceColorHct.Hue, platform == Platform.Phone && isDark ? 26 : 32);
            case Variant.Expressive:
                return TonalPalette.FromHueAndChroma(
                    sourceColorHct.Hue, platform == Platform.Phone ? (isDark ? 36 : 48) : 40);
            case Variant.Vibrant:
                return TonalPalette.FromHueAndChroma(
                    sourceColorHct.Hue, platform == Platform.Phone ? 74 : 56);
            default:
                return base.GetPrimaryPalette(
                    variant, sourceColorHct, isDark, platform, contrastLevel);
        }
    }

    public override TonalPalette GetSecondaryPalette(Variant variant, Hct sourceColorHct, bool isDark, Platform platform, double contrastLevel)
    {
        switch (variant)
        {
            case Variant.Neutral:
                return TonalPalette.FromHueAndChroma(
                    sourceColorHct.Hue,
                    platform == Platform.Phone ? (Hct.IsBlue(sourceColorHct.Hue) ? 6 : 4) :
                                           (Hct.IsBlue(sourceColorHct.Hue) ? 10 : 6));
            case Variant.TonalSpot:
                return TonalPalette.FromHueAndChroma(sourceColorHct.Hue, 16);
            case Variant.Expressive:
                return TonalPalette.FromHueAndChroma(
                    DynamicScheme.GetRotatedHue(
                        sourceColorHct, (double[])[0, 105, 140, 204, 253, 278, 300, 333, 360],
                        (double[])[-160, 155, -100, 96, -96, -156, -165, -160]),
                    platform == Platform.Phone ? (isDark ? 16 : 24) : 24);
            case Variant.Vibrant:
                return TonalPalette.FromHueAndChroma(
                    DynamicScheme.GetRotatedHue(
                        sourceColorHct, (double[])[0, 38, 105, 140, 333, 360],
                        (double[])[-14, 10, -14, 10, -14]),
                    platform == Platform.Phone ? 56 : 36);
            default:
                return base.GetSecondaryPalette(
                    variant, sourceColorHct, isDark, platform, contrastLevel);
        }
    }

    public override TonalPalette GetTertiaryPalette(Variant variant, Hct sourceColorHct, bool isDark, Platform platform, double contrastLevel)
    {
        switch (variant)
        {
            case Variant.Neutral:
                return TonalPalette.FromHueAndChroma(
                    DynamicScheme.GetRotatedHue(
                        sourceColorHct, (double[])[0, 38, 105, 161, 204, 278, 333, 360],
                        (double[])[-32, 26, 10, -39, 24, -15, -32]),
                    platform == Platform.Phone ? 20 : 36);
            case Variant.TonalSpot:
                return TonalPalette.FromHueAndChroma(
                    DynamicScheme.GetRotatedHue(
                        sourceColorHct, (double[])[0, 20, 71, 161, 333, 360],
                        (double[])[-40, 48, -32, 40, -32]),
                    platform == Platform.Phone ? 28 : 32);
            case Variant.Expressive:
                return TonalPalette.FromHueAndChroma(
                    DynamicScheme.GetRotatedHue(
                        sourceColorHct, (double[])[0, 105, 140, 204, 253, 278, 300, 333, 360],
                        (double[])[-165, 160, -105, 101, -101, -160, -170, -165]),
                    48);
            case Variant.Vibrant:
                return TonalPalette.FromHueAndChroma(
                    DynamicScheme.GetRotatedHue(
                        sourceColorHct, (double[])[0, 38, 71, 105, 140, 161, 253, 333, 360],
                        (double[])[-72, 35, 24, -24, 62, 50, 62, -72]),
                    56);
            default:
                return base.GetTertiaryPalette(
                    variant, sourceColorHct, isDark, platform, contrastLevel);
        }
    }

    private static double GetExpressiveNeutralHue(Hct sourceColorHct)
    {
        var hue = DynamicScheme.GetRotatedHue(
            sourceColorHct, (double[])[0, 71, 124, 253, 278, 300, 360],
            (double[])[10, 0, 10, 0, 10, 0]);
        return hue;
    }

    private static double GetExpressiveNeutralChroma(
        Hct sourceColorHct, bool isDark, Platform platform)
    {
        var neutralHue =
            GetExpressiveNeutralHue(
                sourceColorHct);
        return platform == Platform.Phone ?
            (isDark ? (Hct.IsYellow(neutralHue) ? 6 : 14) : 18) :
            12;
    }

    private static double GetVibrantNeutralHue(Hct sourceColorHct)
    {
        return DynamicScheme.GetRotatedHue(
            sourceColorHct, (double[])[0, 38, 105, 140, 333, 360], (double[])[-14, 10, -14, 10, -14]);
    }

    private static double GetVibrantNeutralChroma(
        Hct sourceColorHct, Platform platform)
    {
        var neutralHue =
            GetVibrantNeutralHue(
                sourceColorHct);
        return platform == Platform.Phone ? 28 : (Hct.IsBlue(neutralHue) ? 28 : 20);
    }

    public override TonalPalette GetNeutralPalette(Variant variant, Hct sourceColorHct, bool isDark, Platform platform, double contrastLevel)
    {
        switch (variant)
        {
            case Variant.Neutral:
                return TonalPalette.FromHueAndChroma(
                    sourceColorHct.Hue, platform == Platform.Phone ? 1.4 : 6);
            case Variant.TonalSpot:
                return TonalPalette.FromHueAndChroma(
                    sourceColorHct.Hue, platform == Platform.Phone ? 5 : 10);
            case Variant.Expressive:
                return TonalPalette.FromHueAndChroma(
                    GetExpressiveNeutralHue(
                        sourceColorHct),
                    GetExpressiveNeutralChroma(
                        sourceColorHct, isDark, platform));
            case Variant.Vibrant:
                return TonalPalette.FromHueAndChroma(
                    GetVibrantNeutralHue(
                        sourceColorHct),
                    GetVibrantNeutralChroma(
                        sourceColorHct, platform));
            default:
                return base.GetNeutralPalette(
                    variant, sourceColorHct, isDark, platform, contrastLevel);
        }
    }

    public override TonalPalette GetNeutralVariantPalette(Variant variant, Hct sourceColorHct, bool isDark, Platform platform, double contrastLevel)
    {
        switch (variant)
        {
            case Variant.Neutral:
                return TonalPalette.FromHueAndChroma(
                    sourceColorHct.Hue, (platform == Platform.Phone ? 1.4 : 6) * 2.2);
            case Variant.TonalSpot:
                return TonalPalette.FromHueAndChroma(
                    sourceColorHct.Hue, (platform == Platform.Phone ? 5 : 10) * 1.7);
            case Variant.Expressive:
                var expressiveNeutralHue =
                    GetExpressiveNeutralHue(
                        sourceColorHct);
                var expressiveNeutralChroma =
                    GetExpressiveNeutralChroma(
                        sourceColorHct, isDark, platform);
                return TonalPalette.FromHueAndChroma(
                    expressiveNeutralHue,
                    expressiveNeutralChroma *
                        (expressiveNeutralHue >= 105 && expressiveNeutralHue < 125 ?
                             1.6 :
                             2.3));
            case Variant.Vibrant:
                var vibrantNeutralHue =
                    GetVibrantNeutralHue(
                        sourceColorHct);
                var vibrantNeutralChroma =
                    GetVibrantNeutralChroma(
                        sourceColorHct, platform);
                return TonalPalette.FromHueAndChroma(
                    vibrantNeutralHue, vibrantNeutralChroma * 1.29);
            default:
                return base.GetNeutralVariantPalette(
                    variant, sourceColorHct, isDark, platform, contrastLevel);
        }
    }

    public override TonalPalette? GetErrorPalette(Variant variant, Hct sourceColorHct, bool isDark, Platform platform, double contrastLevel)
    {
        var errorHue = DynamicScheme.GetPiecewiseHue(
            sourceColorHct, (double[])[0, 3, 13, 23, 33, 43, 153, 273, 360],
            (double[])[12, 22, 32, 12, 22, 32, 22, 12]);
        switch (variant)
        {
            case Variant.Neutral:
                return TonalPalette.FromHueAndChroma(
                    errorHue, platform == Platform.Phone ? 50 : 40);
            case Variant.TonalSpot:
                return TonalPalette.FromHueAndChroma(
                    errorHue, platform == Platform.Phone ? 60 : 48);
            case Variant.Expressive:
                return TonalPalette.FromHueAndChroma(
                    errorHue, platform == Platform.Phone ? 64 : 48);
            case Variant.Vibrant:
                return TonalPalette.FromHueAndChroma(
                    errorHue, platform == Platform.Phone ? 80 : 60);
            default:
                return base.GetErrorPalette(
                    variant, sourceColorHct, isDark, platform, contrastLevel);
        }
    }
}
