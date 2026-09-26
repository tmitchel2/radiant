// Copyright 2025 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

namespace Radiant.ColorSystem;

/// <summary>
/// How a <see cref="DynamicColor"/>'s HCT and tone are calculated, which changed with the 2025 spec
/// (chroma multipliers, delta constraints, a different awkward-zone rule). Upstream's
/// <c>ColorCalculationDelegate</c>.
/// </summary>
internal abstract class ColorCalculation
{
    private static readonly ColorCalculation s_spec2021 = new ColorCalculation2021();
    private static readonly ColorCalculation s_spec2025 = new ColorCalculation2025();

    public abstract Hct GetHct(DynamicScheme scheme, DynamicColor color);

    public abstract double GetTone(DynamicScheme scheme, DynamicColor color);

    /// <summary>The calculation for a spec version: 2021's for 2021, 2025's for later ones.</summary>
    public static ColorCalculation For(SpecVersion specVersion) =>
        specVersion == SpecVersion.Spec2021 ? s_spec2021 : s_spec2025;

    // Shared by both specs: a foreground with two backgrounds. The darkest light tone and the
    // lightest dark tone that reach the desired ratio against both; which one wins depends on
    // whether either background prefers a light foreground.
    protected static double AdjustForDualBackgrounds(
        DynamicScheme scheme, DynamicColor color, double answer, double desiredRatio)
    {
        var bgTone1 = color.Background!(scheme)!.GetTone(scheme);
        var bgTone2 = color.SecondBackground!(scheme)!.GetTone(scheme);
        var upper = System.Math.Max(bgTone1, bgTone2);
        var lower = System.Math.Min(bgTone1, bgTone2);

        if (Contrast.RatioOfTones(upper, answer) >= desiredRatio && Contrast.RatioOfTones(lower, answer) >= desiredRatio)
        {
            return answer;
        }

        // The darkest light tone that satisfies the desired ratio, or -1 if such ratio cannot be reached.
        var lightOption = Contrast.Lighter(upper, desiredRatio);

        // The lightest dark tone that satisfies the desired ratio, or -1 if such ratio cannot be reached.
        var darkOption = Contrast.Darker(lower, desiredRatio);

        // Tones suitable for the foreground.
        var availableCount = 0;
        var firstAvailable = 0.0;
        if (lightOption != -1)
        {
            firstAvailable = lightOption;
            availableCount++;
        }
        if (darkOption != -1)
        {
            if (availableCount == 0)
            {
                firstAvailable = darkOption;
            }
            availableCount++;
        }

        var prefersLight = DynamicColor.TonePrefersLightForeground(bgTone1) || DynamicColor.TonePrefersLightForeground(bgTone2);
        if (prefersLight)
        {
            return lightOption < 0 ? 100 : lightOption;
        }
        if (availableCount == 1)
        {
            return firstAvailable;
        }
        return darkOption < 0 ? 0 : darkOption;
    }

    // Whether the color has a background and contrast curve that both resolve for this scheme.
    protected static bool HasResolvedBackground(DynamicScheme scheme, DynamicColor color) =>
        color.Background is not null && color.Background(scheme) is not null
        && color.ContrastCurve is not null && color.ContrastCurve(scheme) is not null;

    protected static bool HasResolvedSecondBackground(DynamicScheme scheme, DynamicColor color) =>
        color.SecondBackground is not null && color.SecondBackground(scheme) is not null;
}
