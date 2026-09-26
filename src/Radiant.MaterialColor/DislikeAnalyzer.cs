// Copyright 2023 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;

namespace Radiant.MaterialColor;

/// <summary>
/// Check and/or fix universally disliked colors.
/// <para>
/// Color science studies of color preference indicate universal distaste for dark yellow-greens,
/// and also show this is correlated to distaste for biological waste and rotting food.
/// </para>
/// <para>
/// See Palmer and Schloss, 2010 or Schloss and Palmer's Chapter 21 in Handbook of Color Psychology
/// (2015).
/// </para>
/// </summary>
public static class DislikeAnalyzer
{
    /// <summary>
    /// Whether a color is disliked: a dark yellow-green that is not neutral.
    /// </summary>
    public static bool IsDisliked(Hct hct)
    {
        ArgumentNullException.ThrowIfNull(hct);
        var huePasses = MathUtils.Round(hct.Hue) >= 90.0 && MathUtils.Round(hct.Hue) <= 111.0;
        var chromaPasses = MathUtils.Round(hct.Chroma) > 16.0;
        var tonePasses = MathUtils.Round(hct.Tone) < 65.0;

        return huePasses && chromaPasses && tonePasses;
    }

    /// <summary>
    /// If a color is disliked, lighten it to make it likable.
    /// </summary>
    /// <returns>A new color if the original color is disliked, or the original color if it is
    /// acceptable.</returns>
    public static Hct FixIfDisliked(Hct hct)
    {
        if (IsDisliked(hct))
        {
            return Hct.From(hct.Hue, hct.Chroma, 70.0);
        }

        return hct;
    }
}
