// Copyright 2022 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Radiant.MaterialColor;

/// <summary>
/// The conditions a theme is resolved under — source color(s), variant, light or dark, contrast
/// level, platform and spec version — together with the tonal palettes they produce. Every
/// <see cref="DynamicColor"/> resolves against a scheme; the role properties (<see cref="Primary"/>,
/// <see cref="OnSurface"/> …) are the resolved colors as ARGB.
/// <para>
/// Prefer the variant subclasses (<see cref="SchemeTonalSpot"/> and so on), which choose the
/// variant; this constructor also lets any palette be supplied directly.
/// </para>
/// </summary>
public class DynamicScheme
{
    /// <summary>The spec version schemes follow when none is given.</summary>
    public const SpecVersion DefaultSpecVersion = SpecVersion.Spec2021;

    /// <summary>The platform schemes are for when none is given.</summary>
    public const Platform DefaultPlatform = Platform.Phone;

    // Resolved colors, keyed weakly by the DynamicColor that produced them, so a color resolves once
    // per scheme and transient colors don't accumulate. Upstream caches the other way round, per
    // color and keyed by scheme; either way it is only a cache.
    private readonly ConditionalWeakTable<DynamicColor, Hct> _hctCache = [];

    /// <summary>A scheme from one source color.</summary>
    public DynamicScheme(
        Hct sourceColorHct,
        Variant variant,
        bool isDark,
        double contrastLevel,
        Platform platform = DefaultPlatform,
        SpecVersion specVersion = DefaultSpecVersion,
        TonalPalette? primaryPalette = null,
        TonalPalette? secondaryPalette = null,
        TonalPalette? tertiaryPalette = null,
        TonalPalette? neutralPalette = null,
        TonalPalette? neutralVariantPalette = null,
        TonalPalette? errorPalette = null)
        : this(
            [sourceColorHct ?? throw new ArgumentNullException(nameof(sourceColorHct))],
            variant, isDark, contrastLevel, platform, specVersion,
            primaryPalette, secondaryPalette, tertiaryPalette, neutralPalette, neutralVariantPalette, errorPalette)
    {
    }

    /// <summary>
    /// A scheme from several source colors. The first is the main one; the others are used by
    /// variants that take more than one (<see cref="Variant.Cmf"/>).
    /// </summary>
    public DynamicScheme(
        IReadOnlyList<Hct> sourceColorHcts,
        Variant variant,
        bool isDark,
        double contrastLevel,
        Platform platform = DefaultPlatform,
        SpecVersion specVersion = DefaultSpecVersion,
        TonalPalette? primaryPalette = null,
        TonalPalette? secondaryPalette = null,
        TonalPalette? tertiaryPalette = null,
        TonalPalette? neutralPalette = null,
        TonalPalette? neutralVariantPalette = null,
        TonalPalette? errorPalette = null)
    {
        ArgumentNullException.ThrowIfNull(sourceColorHcts);
        if (sourceColorHcts.Count == 0)
        {
            throw new ArgumentException("sourceColorHcts cannot be empty", nameof(sourceColorHcts));
        }

        SourceColorHct = sourceColorHcts[0];
        SourceColorHcts = sourceColorHcts.ToArray();
        SourceColorArgb = SourceColorHct.ToInt();
        Variant = variant;
        ContrastLevel = contrastLevel;
        IsDark = isDark;
        Platform = platform;
        SpecVersion = MaybeFallbackSpecVersion(specVersion, variant);

        var palettes = SchemePalettes.For(SpecVersion);
        PrimaryPalette = primaryPalette
            ?? palettes.GetPrimaryPalette(Variant, SourceColorHct, IsDark, Platform, ContrastLevel);
        SecondaryPalette = secondaryPalette
            ?? palettes.GetSecondaryPalette(Variant, SourceColorHct, IsDark, Platform, ContrastLevel);
        TertiaryPalette = tertiaryPalette
            ?? palettes.GetTertiaryPalette(Variant, SourceColorHct, IsDark, Platform, ContrastLevel);
        NeutralPalette = neutralPalette
            ?? palettes.GetNeutralPalette(Variant, SourceColorHct, IsDark, Platform, ContrastLevel);
        NeutralVariantPalette = neutralVariantPalette
            ?? palettes.GetNeutralVariantPalette(Variant, SourceColorHct, IsDark, Platform, ContrastLevel);
        ErrorPalette = errorPalette
            ?? palettes.GetErrorPalette(Variant, SourceColorHct, IsDark, Platform, ContrastLevel)
            ?? TonalPalette.FromHueAndChroma(25.0, 84.0);
    }

    /// <summary>The main source color of the theme.</summary>
    public Hct SourceColorHct { get; }

    /// <summary>All source colors of the theme, the main one first.</summary>
    public IReadOnlyList<Hct> SourceColorHcts { get; }

    /// <summary>The main source color as ARGB.</summary>
    public int SourceColorArgb { get; }

    /// <summary>The variant, or style, of the theme.</summary>
    public Variant Variant { get; }

    /// <summary>Value from -1 to 1. -1 represents minimum contrast, 0 standard, 1 maximum.</summary>
    public double ContrastLevel { get; }

    /// <summary>Whether the scheme is in dark mode.</summary>
    public bool IsDark { get; }

    /// <summary>The platform the scheme is intended for.</summary>
    public Platform Platform { get; }

    /// <summary>
    /// The spec version the scheme follows. Variants the 2025 spec does not cover fall back to 2021,
    /// and 2026 falls back to 2025 for all but <see cref="Variant.Cmf"/>.
    /// </summary>
    public SpecVersion SpecVersion { get; }

    /// <summary>Given a tone, produces a color: hue and chroma of the color are the same as the primary role's.</summary>
    public TonalPalette PrimaryPalette { get; }

    /// <summary>Given a tone, produces a color: hue and chroma of the color are the same as the secondary role's.</summary>
    public TonalPalette SecondaryPalette { get; }

    /// <summary>Given a tone, produces a color: hue and chroma of the color are the same as the tertiary role's.</summary>
    public TonalPalette TertiaryPalette { get; }

    /// <summary>Given a tone, produces a color: hue and chroma of the color are the same as the neutral role's.</summary>
    public TonalPalette NeutralPalette { get; }

    /// <summary>Given a tone, produces a color: hue and chroma of the color are the same as the neutral variant role's.</summary>
    public TonalPalette NeutralVariantPalette { get; }

    /// <summary>Given a tone, produces a reddish, colorful, color.</summary>
    public TonalPalette ErrorPalette { get; }


    private static SpecVersion MaybeFallbackSpecVersion(SpecVersion specVersion, Variant variant)
    {
        if (variant == Variant.Cmf)
        {
            return specVersion;
        }
        if (variant is Variant.Expressive or Variant.Vibrant or Variant.TonalSpot or Variant.Neutral)
        {
            return specVersion == SpecVersion.Spec2026 ? SpecVersion.Spec2025 : specVersion;
        }
        return SpecVersion.Spec2021;
    }

    /// <summary>
    /// The hue for the source color's position among <paramref name="hueBreakpoints"/>: the hue at
    /// the index of the range it falls in, or the source hue if it falls in none.
    /// </summary>
    public static double GetPiecewiseHue(Hct sourceColorHct, IReadOnlyList<double> hueBreakpoints, IReadOnlyList<double> hues)
    {
        ArgumentNullException.ThrowIfNull(sourceColorHct);
        ArgumentNullException.ThrowIfNull(hueBreakpoints);
        ArgumentNullException.ThrowIfNull(hues);
        var size = Math.Min(hueBreakpoints.Count - 1, hues.Count);
        var sourceHue = sourceColorHct.Hue;
        for (var i = 0; i < size; i++)
        {
            if (sourceHue >= hueBreakpoints[i] && sourceHue < hueBreakpoints[i + 1])
            {
                return MathUtils.SanitizeDegreesDouble(hues[i]);
            }
        }
        // No condition matched, return the source hue.
        return sourceHue;
    }

    /// <summary>
    /// The source hue rotated by the rotation for its position among <paramref name="hueBreakpoints"/>.
    /// </summary>
    public static double GetRotatedHue(Hct sourceColorHct, IReadOnlyList<double> hueBreakpoints, IReadOnlyList<double> rotations)
    {
        ArgumentNullException.ThrowIfNull(sourceColorHct);
        ArgumentNullException.ThrowIfNull(hueBreakpoints);
        ArgumentNullException.ThrowIfNull(rotations);
        var rotation = GetPiecewiseHue(sourceColorHct, hueBreakpoints, rotations);
        if (Math.Min(hueBreakpoints.Count - 1, rotations.Count) <= 0)
        {
            // No condition matched, return the source hue.
            rotation = 0;
        }
        return MathUtils.SanitizeDegreesDouble(sourceColorHct.Hue + rotation);
    }

    /// <summary>A dynamic color resolved against this scheme, as ARGB.</summary>
    public int GetArgb(DynamicColor dynamicColor)
    {
        ArgumentNullException.ThrowIfNull(dynamicColor);
        return dynamicColor.GetArgb(this);
    }

    /// <summary>A dynamic color resolved against this scheme, in HCT.</summary>
    public Hct GetHct(DynamicColor dynamicColor)
    {
        ArgumentNullException.ThrowIfNull(dynamicColor);
        return dynamicColor.GetHct(this);
    }

    /// <summary>
    /// This scheme as if it were in another mode and contrast level, keeping its palettes as they
    /// are (some palettes depend on the mode; they are not recomputed). The 2025 spec resolves the
    /// fixed colors this way: as the light, standard-contrast container. Upstream does it with
    /// <c>Object.assign({}, scheme, {isDark, contrastLevel})</c>.
    /// </summary>
    internal DynamicScheme CloneWith(bool isDark, double contrastLevel) => new(
        SourceColorHcts, Variant, isDark, contrastLevel, Platform, SpecVersion,
        PrimaryPalette, SecondaryPalette, TertiaryPalette, NeutralPalette, NeutralVariantPalette, ErrorPalette);

    internal Hct GetOrAddHct(DynamicColor color, Func<DynamicColor, DynamicScheme, Hct> resolve)
    {
        if (_hctCache.TryGetValue(color, out var cached))
        {
            return cached;
        }
        var answer = resolve(color, this);
        _hctCache.AddOrUpdate(color, answer);
        return answer;
    }

    /// <summary>For example <c>Scheme: variant=TonalSpot, mode=light, platform=Phone, contrastLevel=0.0, seed=HCT(282, 48, 40), specVersion=Spec2021</c>.</summary>
    public override string ToString()
    {
        var extraColors = SourceColorHcts.Count <= 1
            ? ""
            : $"sourceColorHctList=[{string.Join(", ", SourceColorHcts.Select(hct => hct.ToString()))}], ";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Scheme: variant={Variant}, mode={(IsDark ? "dark" : "light")}, platform={Platform}, " +
            $"contrastLevel={ContrastLevel:F1}, seed={SourceColorHct}, {extraColors}specVersion={SpecVersion}");
    }

    /// <summary>The <c>primaryPaletteKeyColor</c> role as ARGB.</summary>
    public int PrimaryPaletteKeyColor => GetArgb(MaterialDynamicColors.PrimaryPaletteKeyColor());

    /// <summary>The <c>secondaryPaletteKeyColor</c> role as ARGB.</summary>
    public int SecondaryPaletteKeyColor => GetArgb(MaterialDynamicColors.SecondaryPaletteKeyColor());

    /// <summary>The <c>tertiaryPaletteKeyColor</c> role as ARGB.</summary>
    public int TertiaryPaletteKeyColor => GetArgb(MaterialDynamicColors.TertiaryPaletteKeyColor());

    /// <summary>The <c>neutralPaletteKeyColor</c> role as ARGB.</summary>
    public int NeutralPaletteKeyColor => GetArgb(MaterialDynamicColors.NeutralPaletteKeyColor());

    /// <summary>The <c>neutralVariantPaletteKeyColor</c> role as ARGB.</summary>
    public int NeutralVariantPaletteKeyColor => GetArgb(MaterialDynamicColors.NeutralVariantPaletteKeyColor());

    /// <summary>The <c>errorPaletteKeyColor</c> role as ARGB.</summary>
    public int ErrorPaletteKeyColor => GetArgb(MaterialDynamicColors.ErrorPaletteKeyColor());

    /// <summary>The <c>background</c> role as ARGB.</summary>
    public int Background => GetArgb(MaterialDynamicColors.Background());

    /// <summary>The <c>onBackground</c> role as ARGB.</summary>
    public int OnBackground => GetArgb(MaterialDynamicColors.OnBackground());

    /// <summary>The <c>surface</c> role as ARGB.</summary>
    public int Surface => GetArgb(MaterialDynamicColors.Surface());

    /// <summary>The <c>surfaceDim</c> role as ARGB.</summary>
    public int SurfaceDim => GetArgb(MaterialDynamicColors.SurfaceDim());

    /// <summary>The <c>surfaceBright</c> role as ARGB.</summary>
    public int SurfaceBright => GetArgb(MaterialDynamicColors.SurfaceBright());

    /// <summary>The <c>surfaceContainerLowest</c> role as ARGB.</summary>
    public int SurfaceContainerLowest => GetArgb(MaterialDynamicColors.SurfaceContainerLowest());

    /// <summary>The <c>surfaceContainerLow</c> role as ARGB.</summary>
    public int SurfaceContainerLow => GetArgb(MaterialDynamicColors.SurfaceContainerLow());

    /// <summary>The <c>surfaceContainer</c> role as ARGB.</summary>
    public int SurfaceContainer => GetArgb(MaterialDynamicColors.SurfaceContainer());

    /// <summary>The <c>surfaceContainerHigh</c> role as ARGB.</summary>
    public int SurfaceContainerHigh => GetArgb(MaterialDynamicColors.SurfaceContainerHigh());

    /// <summary>The <c>surfaceContainerHighest</c> role as ARGB.</summary>
    public int SurfaceContainerHighest => GetArgb(MaterialDynamicColors.SurfaceContainerHighest());

    /// <summary>The <c>onSurface</c> role as ARGB.</summary>
    public int OnSurface => GetArgb(MaterialDynamicColors.OnSurface());

    /// <summary>The <c>surfaceVariant</c> role as ARGB.</summary>
    public int SurfaceVariant => GetArgb(MaterialDynamicColors.SurfaceVariant());

    /// <summary>The <c>onSurfaceVariant</c> role as ARGB.</summary>
    public int OnSurfaceVariant => GetArgb(MaterialDynamicColors.OnSurfaceVariant());

    /// <summary>The <c>inverseSurface</c> role as ARGB.</summary>
    public int InverseSurface => GetArgb(MaterialDynamicColors.InverseSurface());

    /// <summary>The <c>inverseOnSurface</c> role as ARGB.</summary>
    public int InverseOnSurface => GetArgb(MaterialDynamicColors.InverseOnSurface());

    /// <summary>The <c>outline</c> role as ARGB.</summary>
    public int Outline => GetArgb(MaterialDynamicColors.Outline());

    /// <summary>The <c>outlineVariant</c> role as ARGB.</summary>
    public int OutlineVariant => GetArgb(MaterialDynamicColors.OutlineVariant());

    /// <summary>The <c>shadow</c> role as ARGB.</summary>
    public int Shadow => GetArgb(MaterialDynamicColors.Shadow());

    /// <summary>The <c>scrim</c> role as ARGB.</summary>
    public int Scrim => GetArgb(MaterialDynamicColors.Scrim());

    /// <summary>The <c>surfaceTint</c> role as ARGB.</summary>
    public int SurfaceTint => GetArgb(MaterialDynamicColors.SurfaceTint());

    /// <summary>The <c>primary</c> role as ARGB.</summary>
    public int Primary => GetArgb(MaterialDynamicColors.Primary());

    /// <summary>The <c>primaryDim</c> role as ARGB. Defined from the 2025 spec on.</summary>
    public int PrimaryDim => GetArgb(MaterialDynamicColors.PrimaryDim()
        ?? throw new InvalidOperationException("`primaryDim` color is undefined prior to 2025 spec."));

    /// <summary>The <c>onPrimary</c> role as ARGB.</summary>
    public int OnPrimary => GetArgb(MaterialDynamicColors.OnPrimary());

    /// <summary>The <c>primaryContainer</c> role as ARGB.</summary>
    public int PrimaryContainer => GetArgb(MaterialDynamicColors.PrimaryContainer());

    /// <summary>The <c>onPrimaryContainer</c> role as ARGB.</summary>
    public int OnPrimaryContainer => GetArgb(MaterialDynamicColors.OnPrimaryContainer());

    /// <summary>The <c>primaryFixed</c> role as ARGB.</summary>
    public int PrimaryFixed => GetArgb(MaterialDynamicColors.PrimaryFixed());

    /// <summary>The <c>primaryFixedDim</c> role as ARGB.</summary>
    public int PrimaryFixedDim => GetArgb(MaterialDynamicColors.PrimaryFixedDim());

    /// <summary>The <c>onPrimaryFixed</c> role as ARGB.</summary>
    public int OnPrimaryFixed => GetArgb(MaterialDynamicColors.OnPrimaryFixed());

    /// <summary>The <c>onPrimaryFixedVariant</c> role as ARGB.</summary>
    public int OnPrimaryFixedVariant => GetArgb(MaterialDynamicColors.OnPrimaryFixedVariant());

    /// <summary>The <c>inversePrimary</c> role as ARGB.</summary>
    public int InversePrimary => GetArgb(MaterialDynamicColors.InversePrimary());

    /// <summary>The <c>secondary</c> role as ARGB.</summary>
    public int Secondary => GetArgb(MaterialDynamicColors.Secondary());

    /// <summary>The <c>secondaryDim</c> role as ARGB. Defined from the 2025 spec on.</summary>
    public int SecondaryDim => GetArgb(MaterialDynamicColors.SecondaryDim()
        ?? throw new InvalidOperationException("`secondaryDim` color is undefined prior to 2025 spec."));

    /// <summary>The <c>onSecondary</c> role as ARGB.</summary>
    public int OnSecondary => GetArgb(MaterialDynamicColors.OnSecondary());

    /// <summary>The <c>secondaryContainer</c> role as ARGB.</summary>
    public int SecondaryContainer => GetArgb(MaterialDynamicColors.SecondaryContainer());

    /// <summary>The <c>onSecondaryContainer</c> role as ARGB.</summary>
    public int OnSecondaryContainer => GetArgb(MaterialDynamicColors.OnSecondaryContainer());

    /// <summary>The <c>secondaryFixed</c> role as ARGB.</summary>
    public int SecondaryFixed => GetArgb(MaterialDynamicColors.SecondaryFixed());

    /// <summary>The <c>secondaryFixedDim</c> role as ARGB.</summary>
    public int SecondaryFixedDim => GetArgb(MaterialDynamicColors.SecondaryFixedDim());

    /// <summary>The <c>onSecondaryFixed</c> role as ARGB.</summary>
    public int OnSecondaryFixed => GetArgb(MaterialDynamicColors.OnSecondaryFixed());

    /// <summary>The <c>onSecondaryFixedVariant</c> role as ARGB.</summary>
    public int OnSecondaryFixedVariant => GetArgb(MaterialDynamicColors.OnSecondaryFixedVariant());

    /// <summary>The <c>tertiary</c> role as ARGB.</summary>
    public int Tertiary => GetArgb(MaterialDynamicColors.Tertiary());

    /// <summary>The <c>tertiaryDim</c> role as ARGB. Defined from the 2025 spec on.</summary>
    public int TertiaryDim => GetArgb(MaterialDynamicColors.TertiaryDim()
        ?? throw new InvalidOperationException("`tertiaryDim` color is undefined prior to 2025 spec."));

    /// <summary>The <c>onTertiary</c> role as ARGB.</summary>
    public int OnTertiary => GetArgb(MaterialDynamicColors.OnTertiary());

    /// <summary>The <c>tertiaryContainer</c> role as ARGB.</summary>
    public int TertiaryContainer => GetArgb(MaterialDynamicColors.TertiaryContainer());

    /// <summary>The <c>onTertiaryContainer</c> role as ARGB.</summary>
    public int OnTertiaryContainer => GetArgb(MaterialDynamicColors.OnTertiaryContainer());

    /// <summary>The <c>tertiaryFixed</c> role as ARGB.</summary>
    public int TertiaryFixed => GetArgb(MaterialDynamicColors.TertiaryFixed());

    /// <summary>The <c>tertiaryFixedDim</c> role as ARGB.</summary>
    public int TertiaryFixedDim => GetArgb(MaterialDynamicColors.TertiaryFixedDim());

    /// <summary>The <c>onTertiaryFixed</c> role as ARGB.</summary>
    public int OnTertiaryFixed => GetArgb(MaterialDynamicColors.OnTertiaryFixed());

    /// <summary>The <c>onTertiaryFixedVariant</c> role as ARGB.</summary>
    public int OnTertiaryFixedVariant => GetArgb(MaterialDynamicColors.OnTertiaryFixedVariant());

    /// <summary>The <c>error</c> role as ARGB.</summary>
    public int Error => GetArgb(MaterialDynamicColors.Error());

    /// <summary>The <c>errorDim</c> role as ARGB. Defined from the 2025 spec on.</summary>
    public int ErrorDim => GetArgb(MaterialDynamicColors.ErrorDim()
        ?? throw new InvalidOperationException("`errorDim` color is undefined prior to 2025 spec."));

    /// <summary>The <c>onError</c> role as ARGB.</summary>
    public int OnError => GetArgb(MaterialDynamicColors.OnError());

    /// <summary>The <c>errorContainer</c> role as ARGB.</summary>
    public int ErrorContainer => GetArgb(MaterialDynamicColors.ErrorContainer());

    /// <summary>The <c>onErrorContainer</c> role as ARGB.</summary>
    public int OnErrorContainer => GetArgb(MaterialDynamicColors.OnErrorContainer());
}
