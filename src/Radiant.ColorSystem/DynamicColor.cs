// Copyright 2022 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;
using ContrastCurveValue = Radiant.ColorSystem.ContrastCurve;
using ToneDeltaPairValue = Radiant.ColorSystem.ToneDeltaPair;

namespace Radiant.ColorSystem;

/// <summary>
/// A color that adjusts itself to the conditions a <see cref="DynamicScheme"/> describes: light or
/// dark, contrast level, variant, spec version.
/// <para>
/// Colors without backgrounds do not change tone when contrast changes. Colors with backgrounds
/// move closer to their background as contrast lowers, and further as it increases.
/// </para>
/// <para>
/// Every member is a function of the scheme rather than a value, which lets a color be defined once
/// and resolve differently for every scheme, and lets later spec versions override single aspects
/// (see <see cref="ExtendSpecVersion"/>).
/// </para>
/// </summary>
public sealed class DynamicColor
{
    private DynamicColor(
        string name,
        Func<DynamicScheme, TonalPalette> palette,
        Func<DynamicScheme, double> tone,
        bool isBackground,
        Func<DynamicScheme, double>? chromaMultiplier,
        Func<DynamicScheme, DynamicColor?>? background,
        Func<DynamicScheme, DynamicColor?>? secondBackground,
        Func<DynamicScheme, ContrastCurveValue?>? contrastCurve,
        Func<DynamicScheme, ToneDeltaPairValue?>? toneDeltaPair)
    {
        if (background is null && secondBackground is not null)
        {
            throw new ArgumentException($"Color {name} has secondBackground defined, but background is not defined.");
        }
        if (background is null && contrastCurve is not null)
        {
            throw new ArgumentException($"Color {name} has contrastCurve defined, but background is not defined.");
        }
        if (background is not null && contrastCurve is null)
        {
            throw new ArgumentException($"Color {name} has background defined, but contrastCurve is not defined.");
        }

        Name = name;
        Palette = palette;
        Tone = tone;
        IsBackground = isBackground;
        ChromaMultiplier = chromaMultiplier;
        Background = background;
        SecondBackground = secondBackground;
        ContrastCurve = contrastCurve;
        ToneDeltaPair = toneDeltaPair;
    }

    /// <summary>The role's name, e.g. <c>primary_container</c>; the 2025 spec matches on it.</summary>
    public string Name { get; }

    /// <summary>The tonal palette the color comes from; its hue and chroma survive contrast adjustment.</summary>
    public Func<DynamicScheme, TonalPalette> Palette { get; }

    /// <summary>The tone before any contrast adjustment.</summary>
    public Func<DynamicScheme, double> Tone { get; }

    /// <summary>Whether this color is a background, with some other color as the foreground.</summary>
    public bool IsBackground { get; }

    /// <summary>A factor the palette's chroma is multiplied by (2025 spec); null means 1.</summary>
    public Func<DynamicScheme, double>? ChromaMultiplier { get; }

    /// <summary>The color this one must contrast with, if any.</summary>
    public Func<DynamicScheme, DynamicColor?>? Background { get; }

    /// <summary>A second color this one must also contrast with, if any.</summary>
    public Func<DynamicScheme, DynamicColor?>? SecondBackground { get; }

    /// <summary>How much contrast against the background is required at each contrast level.</summary>
    public Func<DynamicScheme, ContrastCurveValue?>? ContrastCurve { get; }

    /// <summary>A tone-distance constraint shared with another color, if any.</summary>
    public Func<DynamicScheme, ToneDeltaPairValue?>? ToneDeltaPair { get; }

    /// <summary>A dynamic color defined by a tonal palette and a tone.</summary>
    /// <param name="palette">The palette, as a function of the scheme.</param>
    /// <param name="name">The role's name.</param>
    /// <param name="tone">The tone; defaults to the background's tone, or 50 without one.</param>
    /// <param name="chromaMultiplier">Multiplies the palette's chroma; defaults to 1.</param>
    /// <param name="isBackground">Whether this is a background color.</param>
    /// <param name="background">The color to contrast with.</param>
    /// <param name="secondBackground">A second color to contrast with.</param>
    /// <param name="contrastCurve">Required contrast with the background; needed exactly when a background is.</param>
    /// <param name="toneDeltaPair">A tone-distance constraint with another color.</param>
    public static DynamicColor FromPalette(
        Func<DynamicScheme, TonalPalette> palette,
        string name = "",
        Func<DynamicScheme, double>? tone = null,
        Func<DynamicScheme, double>? chromaMultiplier = null,
        bool isBackground = false,
        Func<DynamicScheme, DynamicColor?>? background = null,
        Func<DynamicScheme, DynamicColor?>? secondBackground = null,
        Func<DynamicScheme, ContrastCurveValue?>? contrastCurve = null,
        Func<DynamicScheme, ToneDeltaPairValue?>? toneDeltaPair = null) => new(
            name,
            palette,
            tone ?? GetInitialToneFromBackground(background),
            isBackground,
            chromaMultiplier,
            background,
            secondBackground,
            contrastCurve,
            toneDeltaPair);

    /// <summary>A tone function that follows the background's tone, or is 50 without one.</summary>
    public static Func<DynamicScheme, double> GetInitialToneFromBackground(Func<DynamicScheme, DynamicColor?>? background)
    {
        if (background is null)
        {
            return _ => 50;
        }
        return s => background(s) is { } color ? color.GetTone(s) : 50;
    }

    /// <summary>
    /// A color that behaves as <paramref name="original"/> for schemes before
    /// <paramref name="specVersion"/> and as <paramref name="extended"/> from it onwards. The two
    /// must share a name and background-ness.
    /// </summary>
    public static DynamicColor ExtendSpecVersion(DynamicColor original, SpecVersion specVersion, DynamicColor extended)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(extended);
        if (original.Name != extended.Name)
        {
            throw new ArgumentException(
                $"Attempting to extend color {original.Name} with color {extended.Name} of different name for spec version {specVersion}.");
        }
        if (original.IsBackground != extended.IsBackground)
        {
            throw new ArgumentException(
                $"Attempting to extend color {original.Name} as a {(original.IsBackground ? "background" : "foreground")} " +
                $"with color {extended.Name} as a {(extended.IsBackground ? "background" : "foreground")} for spec version {specVersion}.");
        }

        DynamicColor Pick(DynamicScheme s) => s.SpecVersion >= specVersion ? extended : original;

        return FromPalette(
            name: original.Name,
            palette: s => Pick(s).Palette(s),
            tone: s => Pick(s).Tone(s),
            isBackground: original.IsBackground,
            chromaMultiplier: s => Pick(s).ChromaMultiplier is { } multiplier ? multiplier(s) : 1,
            background: s => Pick(s).Background is { } background ? background(s) : null,
            secondBackground: s => Pick(s).SecondBackground is { } secondBackground ? secondBackground(s) : null,
            contrastCurve: s => Pick(s).ContrastCurve is { } contrastCurve ? contrastCurve(s) : null,
            toneDeltaPair: s => Pick(s).ToneDeltaPair is { } toneDeltaPair ? toneDeltaPair(s) : null);
    }

    /// <summary>A copy of this color.</summary>
    public DynamicColor Clone() => new(
        Name, Palette, Tone, IsBackground, ChromaMultiplier, Background, SecondBackground, ContrastCurve, ToneDeltaPair);

    /// <summary>
    /// A copy of this color under another name, optionally with another tone function; every other
    /// aspect (palette, backgrounds, contrast, delta pair) is shared. Upstream does this with
    /// <c>Object.assign(color.clone(), {name, tone})</c>, e.g. to define <c>surface_tint</c> as
    /// <c>primary</c>.
    /// </summary>
    public DynamicColor With(string? name = null, Func<DynamicScheme, double>? tone = null) => new(
        name ?? Name, Palette, tone ?? Tone, IsBackground, ChromaMultiplier, Background, SecondBackground, ContrastCurve, ToneDeltaPair);

    /// <summary>The color, as ARGB, under the conditions in <paramref name="scheme"/>.</summary>
    public int GetArgb(DynamicScheme scheme) => GetHct(scheme).ToInt();

    /// <summary>
    /// The color, in HCT, under the conditions in <paramref name="scheme"/>. Cached per scheme (the
    /// scheme holds the cache, weakly keyed by color, so it is safe across threads).
    /// </summary>
    public Hct GetHct(DynamicScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        return scheme.GetOrAddHct(this, static (color, s) => ColorCalculation.For(s.SpecVersion).GetHct(s, color));
    }

    /// <summary>The tone, T in HCT, of this color under the conditions in <paramref name="scheme"/>.</summary>
    public double GetTone(DynamicScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        return ColorCalculation.For(scheme.SpecVersion).GetTone(scheme, this);
    }

    /// <summary>
    /// Given a background tone, the foreground tone whose contrast ratio with it is as close to
    /// <paramref name="ratio"/> as possible.
    /// </summary>
    /// <param name="bgTone">Tone in HCT, 0 to 100; undefined behavior outside that range.</param>
    /// <param name="ratio">The contrast ratio desired between the background and the result.</param>
    public static double ForegroundTone(double bgTone, double ratio)
    {
        var lighterTone = Contrast.LighterUnsafe(bgTone, ratio);
        var darkerTone = Contrast.DarkerUnsafe(bgTone, ratio);
        var lighterRatio = Contrast.RatioOfTones(lighterTone, bgTone);
        var darkerRatio = Contrast.RatioOfTones(darkerTone, bgTone);
        var preferLighter = TonePrefersLightForeground(bgTone);

        if (preferLighter)
        {
            // This handles an edge case where the initial contrast ratio is high (ex. 13.0), and the
            // ratio passed to the function is that high ratio, and both the lighter and darker ratio
            // fails to pass that ratio.
            //
            // This was observed with Tonal Spot's On Primary Container turning black momentarily
            // between high and max contrast in light mode. PC's standard tone was T90, OPC's was
            // T10, it was light mode, and the contrast value was 0.6568521221032331.
            var negligibleDifference = Math.Abs(lighterRatio - darkerRatio) < 0.1 && lighterRatio < ratio && darkerRatio < ratio;
            return lighterRatio >= ratio || lighterRatio >= darkerRatio || negligibleDifference ? lighterTone : darkerTone;
        }
        else
        {
            return darkerRatio >= ratio || darkerRatio >= lighterRatio ? darkerTone : lighterTone;
        }
    }

    /// <summary>
    /// Whether <paramref name="tone"/> prefers a light foreground. People prefer white foregrounds
    /// on ~T60–70 (observed over time, and by Andrew Somers during research for APCA). T60 is used
    /// to make the smallest discontinuity when skipping down to T49 to ensure light foregrounds;
    /// since the dark monochrome scheme's tertiary container needs tone 60, 60 itself is excluded.
    /// </summary>
    public static bool TonePrefersLightForeground(double tone) => MathUtils.Round(tone) < 60.0;

    /// <summary>Whether <paramref name="tone"/> can reach a contrast ratio of 4.5 with a lighter color.</summary>
    public static bool ToneAllowsLightForeground(double tone) => MathUtils.Round(tone) <= 49.0;

    /// <summary>Adjusts a tone so white has 4.5 contrast with it, if it is reasonably close to supporting it.</summary>
    public static double EnableLightForeground(double tone)
    {
        if (TonePrefersLightForeground(tone) && !ToneAllowsLightForeground(tone))
        {
            return 49.0;
        }
        return tone;
    }

    /// <summary>The role name, for debugging.</summary>
    public override string ToString() => Name;
}
