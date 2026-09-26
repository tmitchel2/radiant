// Copyright 2021 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Radiant.MaterialColor;

/// <summary>
/// Given a large set of colors, remove colors that are unsuitable for a UI theme, and rank the rest
/// based on suitability.
/// <para>
/// Enables use of a high cluster count for image quantization, thus ensuring colors aren't muddied,
/// while curating the high cluster count to a much smaller number of appropriate choices.
/// </para>
/// </summary>
public static class Score
{
    private const double TargetChroma = 48.0; // A1 Chroma
    private const double WeightProportion = 0.7;
    private const double WeightChromaAbove = 0.3;
    private const double WeightChromaBelow = 0.1;
    private const double CutoffChroma = 5.0;
    private const double CutoffExcitedProportion = 0.01;

    /// <summary>
    /// Given a map with keys of colors and values of how often the color appears, rank the colors
    /// based on suitability for being used for a UI theme. Upstream's <c>Score.score</c>.
    /// </summary>
    /// <param name="colorsToPopulation">
    /// Map with keys of colors and values of how often the color appears, usually from a source
    /// image. Its enumeration order breaks ties between equal scores.
    /// </param>
    /// <param name="desired">
    /// Max count of colors to be returned in the list. 4 matches the Android wallpaper picker.
    /// </param>
    /// <param name="fallbackColorArgb">
    /// Color to be returned if no other options are available; by default Google Blue.
    /// </param>
    /// <param name="filter">
    /// Whether to filter out hues that are not used often enough, and colors that are effectively
    /// grayscale.
    /// </param>
    /// <returns>
    /// Colors sorted by suitability for a UI theme. The most suitable color is the first item, the
    /// least suitable is the last. There will always be at least one color returned. If all the input
    /// colors were not suitable for a theme, the fallback color will be provided.
    /// </returns>
    public static IReadOnlyList<int> Rank(
        IReadOnlyDictionary<int, int> colorsToPopulation,
        int desired = 4,
        int fallbackColorArgb = unchecked((int)0xff4285f4),
        bool filter = true)
    {
        ArgumentNullException.ThrowIfNull(colorsToPopulation);

        // Get the HCT color for each Argb value, while finding the per hue count and
        // total count.
        var colorsHct = new List<Hct>();
        var huePopulation = new int[360];
        var populationSum = 0.0;
        foreach (var (argb, population) in colorsToPopulation)
        {
            var hct = Hct.FromInt(argb);
            colorsHct.Add(hct);
            var hue = (int)Math.Floor(hct.Hue);
            huePopulation[hue] += population;
            populationSum += population;
        }

        // Hues with more usage in neighboring 30 degree slice get a larger number.
        var hueExcitedProportions = new double[360];
        for (var hue = 0; hue < 360; hue++)
        {
            var proportion = huePopulation[hue] / populationSum;
            for (var i = hue - 14; i < hue + 16; i++)
            {
                var neighborHue = MathUtils.SanitizeDegreesInt(i);
                hueExcitedProportions[neighborHue] += proportion;
            }
        }

        // Scores each HCT color based on usage and chroma, while optionally
        // filtering out values that do not have enough chroma or usage.
        var scoredHcts = new List<(Hct Hct, double Score)>();
        foreach (var hct in colorsHct)
        {
            var hue = MathUtils.SanitizeDegreesInt((int)MathUtils.Round(hct.Hue));
            var proportion = hueExcitedProportions[hue];
            if (filter && (hct.Chroma < CutoffChroma || proportion <= CutoffExcitedProportion))
            {
                continue;
            }

            var proportionScore = proportion * 100.0 * WeightProportion;
            var chromaWeight = hct.Chroma < TargetChroma ? WeightChromaBelow : WeightChromaAbove;
            var chromaScore = (hct.Chroma - TargetChroma) * chromaWeight;
            var score = proportionScore + chromaScore;
            scoredHcts.Add((hct, score));
        }
        // Sorted so that colors with higher scores come first. Upstream's sorts are stable (Java's
        // Collections.sort, JavaScript's Array.prototype.sort), so equal scores keep the input's
        // order; OrderByDescending is stable too, where List.Sort is not.
        var sortedHcts = scoredHcts.OrderByDescending(entry => entry.Score).ToList();

        // Iterates through potential hue differences in degrees in order to select
        // the colors with the largest distribution of hues possible. Starting at
        // 90 degrees(maximum difference for 4 colors) then decreasing down to a
        // 15 degree minimum.
        var chosenColors = new List<Hct>();
        for (var differenceDegrees = 90; differenceDegrees >= 15; differenceDegrees--)
        {
            chosenColors.Clear();
            foreach (var (hct, _) in sortedHcts)
            {
                var hasDuplicateHue = false;
                foreach (var chosenHct in chosenColors)
                {
                    if (MathUtils.DifferenceDegrees(hct.Hue, chosenHct.Hue) < differenceDegrees)
                    {
                        hasDuplicateHue = true;
                        break;
                    }
                }
                if (!hasDuplicateHue)
                {
                    chosenColors.Add(hct);
                }
                if (chosenColors.Count >= desired)
                {
                    break;
                }
            }
            if (chosenColors.Count >= desired)
            {
                break;
            }
        }
        var colors = new List<int>();
        if (chosenColors.Count == 0)
        {
            colors.Add(fallbackColorArgb);
        }
        foreach (var chosenHct in chosenColors)
        {
            colors.Add(chosenHct.ToInt());
        }
        return colors;
    }
}
