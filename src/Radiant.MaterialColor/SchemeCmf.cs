// Copyright 2026 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;
using System.Collections.Generic;

namespace Radiant.MaterialColor;

/// <summary>
/// A Dynamic Color theme with two source colors: the second (when given) becomes the tertiary, and
/// the error hue is chosen to stand apart from both. Only defined by the 2026 spec.
/// </summary>
public sealed class SchemeCmf : DynamicScheme
{
    /// <summary>A CMF scheme from one source color.</summary>
    /// <exception cref="ArgumentException"><paramref name="specVersion"/> is not 2026.</exception>
    public SchemeCmf(
        Hct sourceColorHct,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = SpecVersion.Spec2026,
        Platform platform = DefaultPlatform)
        : this([sourceColorHct ?? throw new ArgumentNullException(nameof(sourceColorHct))], isDark, contrastLevel, specVersion, platform)
    {
    }

    /// <summary>A CMF scheme from a primary source color and, optionally, a second one.</summary>
    /// <exception cref="ArgumentException"><paramref name="specVersion"/> is not 2026.</exception>
    public SchemeCmf(
        IReadOnlyList<Hct> sourceColorHcts,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion = SpecVersion.Spec2026,
        Platform platform = DefaultPlatform)
        : this(sourceColorHcts, isDark, contrastLevel, specVersion, platform, CreatePalettes(sourceColorHcts, specVersion))
    {
    }

    private SchemeCmf(
        IReadOnlyList<Hct> sourceColorHcts,
        bool isDark,
        double contrastLevel,
        SpecVersion specVersion,
        Platform platform,
        CmfPalettes palettes)
        : base(
            sourceColorHcts, Variant.Cmf, isDark, contrastLevel, platform, specVersion,
            palettes.Primary, palettes.Secondary, palettes.Tertiary, palettes.Neutral, palettes.NeutralVariant, palettes.Error)
    {
    }

    /// <summary>The error hue for a primary and a tertiary hue: one that stands apart from both.</summary>
    public static double GetErrorHue(double primaryHue, double tertiaryHue)
    {
        if (primaryHue <= 8)
        {
            return tertiaryHue <= 24 ? 28 : (tertiaryHue <= 32 ? 16 : 20);
        }
        else if (primaryHue <= 16)
        {
            return tertiaryHue <= 24 ? 32 : (tertiaryHue <= 32 ? 20 : 24);
        }
        else if (primaryHue <= 20)
        {
            return tertiaryHue <= 28 ? 32 : (tertiaryHue <= 32 ? 24 : 28);
        }
        else if (primaryHue <= 28)
        {
            return tertiaryHue <= 24 ? 32 : 16;
        }
        else if (primaryHue <= 32)
        {
            return tertiaryHue <= 20 ? 24 : (tertiaryHue <= 28 ? 16 : 20);
        }
        else if (primaryHue <= 40)
        {
            return tertiaryHue > 20 && tertiaryHue <= 28 ? 16 : 24;
        }
        else if (primaryHue <= 152)
        {
            return tertiaryHue > 24 && tertiaryHue <= 36 ? 20 : 32;
        }
        else if (primaryHue <= 272)
        {
            return tertiaryHue > 20 && tertiaryHue <= 28 ? 16 : 24;
        }
        else
        {
            return tertiaryHue > 12 && tertiaryHue <= 28 ? 32 : 16;
        }
    }

    private static CmfPalettes CreatePalettes(IReadOnlyList<Hct> sourceColorHcts, SpecVersion specVersion)
    {
        ArgumentNullException.ThrowIfNull(sourceColorHcts);
        if (specVersion != SpecVersion.Spec2026)
        {
            throw new ArgumentException("SchemeCmf can only be used with spec version 2026.", nameof(specVersion));
        }
        if (sourceColorHcts.Count == 0)
        {
            throw new ArgumentException("sourceColorHcts cannot be empty", nameof(sourceColorHcts));
        }

        var sourceColorHct = sourceColorHcts[0];
        var secondarySourceColorHct = sourceColorHcts.Count > 1 ? sourceColorHcts[1] : sourceColorHct;

        return new CmfPalettes(
            Primary: TonalPalette.FromHueAndChroma(sourceColorHct.Hue, sourceColorHct.Chroma),
            Secondary: TonalPalette.FromHueAndChroma(sourceColorHct.Hue, sourceColorHct.Chroma * 0.5),
            Tertiary: sourceColorHct.ToInt() == secondarySourceColorHct.ToInt()
                ? TonalPalette.FromHueAndChroma(sourceColorHct.Hue, sourceColorHct.Chroma * 0.75)
                : TonalPalette.FromHueAndChroma(secondarySourceColorHct.Hue, secondarySourceColorHct.Chroma),
            Neutral: TonalPalette.FromHueAndChroma(sourceColorHct.Hue, sourceColorHct.Chroma * 0.2),
            NeutralVariant: TonalPalette.FromHueAndChroma(sourceColorHct.Hue, sourceColorHct.Chroma * 0.2),
            Error: TonalPalette.FromHueAndChroma(
                GetErrorHue(sourceColorHct.Hue, secondarySourceColorHct.Hue),
                Math.Max(sourceColorHct.Chroma, 50.0)));
    }

    private readonly record struct CmfPalettes(
        TonalPalette Primary,
        TonalPalette Secondary,
        TonalPalette Tertiary,
        TonalPalette Neutral,
        TonalPalette NeutralVariant,
        TonalPalette Error);
}
