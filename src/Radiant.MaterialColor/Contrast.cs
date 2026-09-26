// Copyright 2022 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;

namespace Radiant.MaterialColor;

/// <summary>
/// Utility methods for calculating contrast given two colors, or calculating a color given one
/// color and a contrast ratio.
/// <para>
/// Contrast ratio is calculated using XYZ's Y. When linearized to match human perception, Y becomes
/// HCT's tone and L*a*b*'s L*. Informally, this is the lightness of a color.
/// </para>
/// <para>
/// Methods refer to tone, T in the HCT color space. Tone is equivalent to L* in the L*a*b* color
/// space, or L in the LCH color space.
/// </para>
/// </summary>
public static class Contrast
{
    /// <summary>
    /// A contrast ratio, which ranges from 1 to 21. Tones are between 0 and 100; values outside are
    /// clamped.
    /// </summary>
    public static double RatioOfTones(double toneA, double toneB)
    {
        toneA = MathUtils.ClampDouble(0.0, 100.0, toneA);
        toneB = MathUtils.ClampDouble(0.0, 100.0, toneB);
        return RatioOfYs(ColorUtils.YFromLstar(toneA), ColorUtils.YFromLstar(toneB));
    }

    /// <summary>The contrast ratio of two relative luminances (Y in XYZ, 0 to 100).</summary>
    public static double RatioOfYs(double y1, double y2)
    {
        var lighter = y1 > y2 ? y1 : y2;
        var darker = (lighter == y2) ? y1 : y2;
        return (lighter + 5.0) / (darker + 5.0);
    }

    /// <summary>
    /// A tone &gt;= <paramref name="tone"/> that ensures <paramref name="ratio"/>, between 0 and 100;
    /// or -1 if the ratio cannot be achieved with the tone.
    /// </summary>
    /// <param name="tone">Tone the return value must contrast with. Range is 0 to 100; invalid values
    /// result in -1 being returned.</param>
    /// <param name="ratio">Contrast ratio of the return value and tone. Range is 1 to 21; invalid
    /// values have undefined behavior.</param>
    public static double Lighter(double tone, double ratio)
    {
        if (tone < 0.0 || tone > 100.0)
        {
            return -1.0;
        }

        var darkY = ColorUtils.YFromLstar(tone);
        var lightY = ratio * (darkY + 5.0) - 5.0;
        var realContrast = RatioOfYs(lightY, darkY);
        var delta = Math.Abs(realContrast - ratio);
        if (realContrast < ratio && delta > 0.04)
        {
            return -1;
        }

        // Ensure gamut mapping, which requires a 'range' on tone, will still result
        // the correct ratio by darkening slightly.
        var returnValue = ColorUtils.LstarFromY(lightY) + 0.4;
        if (returnValue < 0 || returnValue > 100)
        {
            return -1;
        }
        return returnValue;
    }

    /// <summary>
    /// A tone &lt;= <paramref name="tone"/> that ensures <paramref name="ratio"/>, between 0 and 100;
    /// or -1 if the ratio cannot be achieved with the tone.
    /// </summary>
    /// <param name="tone">Tone the return value must contrast with. Range is 0 to 100; invalid values
    /// result in -1 being returned.</param>
    /// <param name="ratio">Contrast ratio of the return value and tone. Range is 1 to 21; invalid
    /// values have undefined behavior.</param>
    public static double Darker(double tone, double ratio)
    {
        if (tone < 0.0 || tone > 100.0)
        {
            return -1.0;
        }

        var lightY = ColorUtils.YFromLstar(tone);
        var darkY = ((lightY + 5.0) / ratio) - 5.0;
        var realContrast = RatioOfYs(lightY, darkY);

        var delta = Math.Abs(realContrast - ratio);
        if (realContrast < ratio && delta > 0.04)
        {
            return -1;
        }

        // Ensure gamut mapping, which requires a 'range' on tone, will still result
        // the correct ratio by darkening slightly.
        var returnValue = ColorUtils.LstarFromY(darkY) - 0.4;
        if (returnValue < 0 || returnValue > 100)
        {
            return -1;
        }
        return returnValue;
    }

    /// <summary>
    /// A tone &gt;= <paramref name="tone"/> that ensures <paramref name="ratio"/>, between 0 and 100;
    /// or 100 if the ratio cannot be achieved with the tone.
    /// <para>
    /// Unsafe because, although the returned value is guaranteed to be in bounds for tone (0 to
    /// 100), it may not reach the ratio with the tone: for example, there is no color lighter than
    /// T100.
    /// </para>
    /// </summary>
    /// <param name="tone">Tone the return value must contrast with. Range is 0 to 100; invalid values
    /// result in 100 being returned.</param>
    /// <param name="ratio">Desired contrast ratio of the return value and tone. Range is 1 to 21;
    /// invalid values have undefined behavior.</param>
    public static double LighterUnsafe(double tone, double ratio)
    {
        var lighterSafe = Lighter(tone, ratio);
        return (lighterSafe < 0.0) ? 100.0 : lighterSafe;
    }

    /// <summary>
    /// A tone &lt;= <paramref name="tone"/> that ensures <paramref name="ratio"/>, between 0 and 100;
    /// or 0 if the ratio cannot be achieved with the tone.
    /// <para>
    /// Unsafe because, although the returned value is guaranteed to be in bounds for tone (0 to
    /// 100), it may not reach the ratio with the tone: for example, there is no color darker than
    /// T0.
    /// </para>
    /// </summary>
    /// <param name="tone">Tone the return value must contrast with. Range is 0 to 100; invalid values
    /// result in 0 being returned.</param>
    /// <param name="ratio">Desired contrast ratio of the return value and tone. Range is 1 to 21;
    /// invalid values have undefined behavior.</param>
    public static double DarkerUnsafe(double tone, double ratio)
    {
        var darkerSafe = Darker(tone, ratio);
        return (darkerSafe < 0.0) ? 0.0 : darkerSafe;
    }
}
