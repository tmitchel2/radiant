// Copyright 2023 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Radiant.MaterialColor;

/// <summary>
/// Design utilities using color temperature theory.
/// <para>
/// Analogous colors, complementary color, and cache to efficiently, lazily, generate data for
/// calculations when needed. The lazily computed data is safe to use from several threads at once.
/// </para>
/// <para>
/// Colors are identified by reference, as upstream's maps key them: <see cref="RelativeTemperature"/>
/// and <see cref="TempsByHct"/> know only the <see cref="Hct"/> instances in
/// <see cref="HctsByHue"/> and <see cref="Input"/>.
/// </para>
/// </summary>
public sealed class TemperatureCache
{
    private readonly Lazy<ReadOnlyCollection<Hct>> _hctsByHue;
    private readonly Lazy<ReadOnlyDictionary<Hct, double>> _tempsByHct;
    private readonly Lazy<ReadOnlyCollection<Hct>> _hctsByTemp;
    private readonly Lazy<double> _inputRelativeTemperature;
    private readonly Lazy<Hct> _complement;

    /// <summary>A cache for the temperature theory of <paramref name="input"/>.</summary>
    public TemperatureCache(Hct input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Input = input;
        _hctsByHue = new Lazy<ReadOnlyCollection<Hct>>(ComputeHctsByHue);
        _tempsByHct = new Lazy<ReadOnlyDictionary<Hct, double>>(ComputeTempsByHct);
        _hctsByTemp = new Lazy<ReadOnlyCollection<Hct>>(ComputeHctsByTemp);
        _inputRelativeTemperature = new Lazy<double>(() => RelativeTemperature(Input));
        _complement = new Lazy<Hct>(ComputeComplement);
    }

    /// <summary>The color whose temperature theory this cache holds.</summary>
    public Hct Input { get; }

    /// <summary>
    /// <see cref="HctsByHue"/> and <see cref="Input"/>, sorted by raw temperature, coldest first.
    /// Colors of equal temperature keep their order in hue (then the input last).
    /// </summary>
    public IReadOnlyList<Hct> HctsByTemp => _hctsByTemp.Value;

    /// <summary>The warmest color with the same chroma and tone as the input.</summary>
    public Hct Warmest => _hctsByTemp.Value[_hctsByTemp.Value.Count - 1];

    /// <summary>The coldest color with the same chroma and tone as the input.</summary>
    public Hct Coldest => _hctsByTemp.Value[0];

    /// <summary>
    /// A color that complements the input color aesthetically.
    /// <para>
    /// In art, this is usually described as being across the color wheel. History of this shows
    /// intent as a color that is just as cool-warm as the input color is warm-cool.
    /// </para>
    /// </summary>
    public Hct Complement => _complement.Value;

    /// <summary>Relative temperature of the input color. See <see cref="RelativeTemperature"/>.</summary>
    public double InputRelativeTemperature => _inputRelativeTemperature.Value;

    /// <summary>A map with keys of HCTs in <see cref="HctsByTemp"/>, values of raw temperature.</summary>
    public IReadOnlyDictionary<Hct, double> TempsByHct => _tempsByHct.Value;

    /// <summary>
    /// HCTs for all hues, with the same chroma/tone as the input. Sorted ascending, hue 0 to 360
    /// (both ends included, so 361 colors).
    /// </summary>
    public IReadOnlyList<Hct> HctsByHue => _hctsByHue.Value;

    /// <summary>
    /// A set of colors with differing hues, equidistant in temperature.
    /// <para>
    /// In art, this is usually described as a set of 5 colors on a color wheel divided into 12
    /// sections. This method allows provision of either of those values.
    /// </para>
    /// <para>
    /// Behavior is undefined when <paramref name="count"/> or <paramref name="divisions"/> is 0.
    /// When divisions &lt; count, colors repeat.
    /// </para>
    /// </summary>
    /// <param name="count">The number of colors to return, includes the input color.</param>
    /// <param name="divisions">The number of divisions on the color wheel.</param>
    public IReadOnlyList<Hct> Analogous(int count = 5, int divisions = 12)
    {
        var hctsByHue = _hctsByHue.Value;
        var startHue = (int)MathUtils.Round(Input.Hue);
        var startHct = hctsByHue[startHue];
        var lastTemp = RelativeTemperature(startHct);
        var allColors = new List<Hct> { startHct };

        var absoluteTotalTempDelta = 0.0;
        for (var i = 0; i < 360; i++)
        {
            var hue = MathUtils.SanitizeDegreesInt(startHue + i);
            var hct = hctsByHue[hue];
            var temp = RelativeTemperature(hct);
            var tempDelta = Math.Abs(temp - lastTemp);
            lastTemp = temp;
            absoluteTotalTempDelta += tempDelta;
        }
        var hueAddend = 1;
        var tempStep = absoluteTotalTempDelta / divisions;
        var totalTempDelta = 0.0;
        lastTemp = RelativeTemperature(startHct);
        while (allColors.Count < divisions)
        {
            var hue = MathUtils.SanitizeDegreesInt(startHue + hueAddend);
            var hct = hctsByHue[hue];
            var temp = RelativeTemperature(hct);
            var tempDelta = Math.Abs(temp - lastTemp);
            totalTempDelta += tempDelta;

            var desiredTotalTempDeltaForIndex = allColors.Count * tempStep;
            var indexSatisfied = totalTempDelta >= desiredTotalTempDeltaForIndex;
            var indexAddend = 1;
            // Keep adding this hue to the answers until its temperature is
            // insufficient. This ensures consistent behavior when there aren't
            // [divisions] discrete steps between 0 and 360 in hue with [tempStep]
            // delta in temperature between them.
            //
            // For example, white and black have no analogues: there are no other
            // colors at T100/T0. Therefore, they should just be added to the array
            // as answers.
            while (indexSatisfied && allColors.Count < divisions)
            {
                allColors.Add(hct);
                var nextDesiredTotalTempDeltaForIndex = (allColors.Count + indexAddend) * tempStep;
                indexSatisfied = totalTempDelta >= nextDesiredTotalTempDeltaForIndex;
                indexAddend++;
            }
            lastTemp = temp;
            hueAddend++;
            if (hueAddend > 360)
            {
                while (allColors.Count < divisions)
                {
                    allColors.Add(hct);
                }
                break;
            }
        }

        var answers = new List<Hct> { Input };

        // First, generate analogues from rotating counter-clockwise.
        var increaseHueCount = (int)Math.Floor((count - 1) / 2.0);
        for (var i = 1; i < (increaseHueCount + 1); i++)
        {
            var index = 0 - i;
            while (index < 0)
            {
                index = allColors.Count + index;
            }
            if (index >= allColors.Count)
            {
                index %= allColors.Count;
            }
            answers.Insert(0, allColors[index]);
        }

        // Second, generate analogues from rotating clockwise.
        var decreaseHueCount = count - increaseHueCount - 1;
        for (var i = 1; i < (decreaseHueCount + 1); i++)
        {
            var index = i;
            while (index < 0)
            {
                index = allColors.Count + index;
            }
            if (index >= allColors.Count)
            {
                index %= allColors.Count;
            }
            answers.Add(allColors[index]);
        }

        return answers;
    }

    /// <summary>
    /// Temperature relative to all colors with the same chroma and tone. Value on a scale from 0 to
    /// 1.
    /// </summary>
    /// <param name="hct">One of the colors in <see cref="HctsByTemp"/> (by reference).</param>
    /// <exception cref="ArgumentException"><paramref name="hct"/> is not one of them (upstream
    /// returns NaN).</exception>
    public double RelativeTemperature(Hct hct)
    {
        ArgumentNullException.ThrowIfNull(hct);
        var tempsByHct = _tempsByHct.Value;
        if (!tempsByHct.TryGetValue(hct, out var temp))
        {
            throw new ArgumentException("Not one of this cache's colors (they are compared by reference).", nameof(hct));
        }
        var range = tempsByHct[Warmest] - tempsByHct[Coldest];
        var differenceFromColdest = temp - tempsByHct[Coldest];
        // Handle when there's no difference in temperature between warmest and
        // coldest: for example, at T100, only one color is available, white.
        if (range == 0.0)
        {
            return 0.5;
        }
        return differenceFromColdest / range;
    }

    /// <summary>Determines if an angle is between two other angles, rotating clockwise.</summary>
    public static bool IsBetween(double angle, double a, double b)
    {
        if (a < b)
        {
            return a <= angle && angle <= b;
        }
        return a <= angle || angle <= b;
    }

    /// <summary>
    /// Value representing cool-warm factor of a color. Values below 0 are considered cool, above,
    /// warm.
    /// <para>
    /// Color science has researched emotion and harmony, which art uses to select colors. Warm-cool
    /// is the foundation of analogous and complementary colors. See Li-Chen Ou's Chapter 19 in
    /// Handbook of Color Psychology (2015), and Josef Albers' Interaction of Color chapters 19 and
    /// 21.
    /// </para>
    /// <para>
    /// Implementation of Ou, Woodcock and Wright's algorithm, which uses L*a*b* / LCH color space.
    /// Return value has these properties: values below 0 are cool, above 0 are warm. Lower bound:
    /// -0.52 - (chroma ^ 1.07 / 20); L*a*b* chroma is infinite, so assuming a max of 130 chroma,
    /// -9.66. Upper bound: -0.52 + (chroma ^ 1.07 / 20); assuming a max of 130 chroma, 8.61.
    /// </para>
    /// </summary>
    public static double RawTemperature(Hct color)
    {
        ArgumentNullException.ThrowIfNull(color);
        var lab = ColorUtils.LabFromArgb(color.ToInt());
        var hue = MathUtils.SanitizeDegreesDouble(Math.Atan2(lab[2], lab[1]) * 180.0 / Math.PI);
        var chroma = Math.Sqrt((lab[1] * lab[1]) + (lab[2] * lab[2]));
        var temperature = -0.5 +
            0.02 * Math.Pow(chroma, 1.07) *
                Math.Cos(MathUtils.SanitizeDegreesDouble(hue - 50.0) * Math.PI / 180.0);
        return temperature;
    }

    private ReadOnlyCollection<Hct> ComputeHctsByHue()
    {
        var hcts = new List<Hct>();
        for (var hue = 0.0; hue <= 360.0; hue += 1.0)
        {
            var colorAtHue = Hct.From(hue, Input.Chroma, Input.Tone);
            hcts.Add(colorAtHue);
        }
        return hcts.AsReadOnly();
    }

    private ReadOnlyDictionary<Hct, double> ComputeTempsByHct()
    {
        var temperaturesByHct = new Dictionary<Hct, double>(ReferenceEqualityComparer.Instance);
        foreach (var e in _hctsByHue.Value.Append(Input))
        {
            temperaturesByHct[e] = RawTemperature(e);
        }
        return temperaturesByHct.AsReadOnly();
    }

    private ReadOnlyCollection<Hct> ComputeHctsByTemp()
    {
        var temperaturesByHct = _tempsByHct.Value;
        // A stable sort, as JavaScript's Array.prototype.sort is: colors of equal temperature keep
        // their order.
        return _hctsByHue.Value.Append(Input).OrderBy(hct => temperaturesByHct[hct]).ToList().AsReadOnly();
    }

    private Hct ComputeComplement()
    {
        var hctsByHue = _hctsByHue.Value;
        var tempsByHct = _tempsByHct.Value;
        var coldestHue = Coldest.Hue;
        var coldestTemp = tempsByHct[Coldest];

        var warmestHue = Warmest.Hue;
        var warmestTemp = tempsByHct[Warmest];
        var range = warmestTemp - coldestTemp;
        var startHueIsColdestToWarmest = IsBetween(Input.Hue, coldestHue, warmestHue);
        var startHue = startHueIsColdestToWarmest ? warmestHue : coldestHue;
        var endHue = startHueIsColdestToWarmest ? coldestHue : warmestHue;
        const double directionOfRotation = 1.0;
        var smallestError = 1000.0;
        var answer = hctsByHue[(int)MathUtils.Round(Input.Hue)];

        var complementRelativeTemp = 1.0 - InputRelativeTemperature;
        // Find the color in the other section, closest to the inverse percentile
        // of the input color. This is the complement.
        for (var hueAddend = 0.0; hueAddend <= 360.0; hueAddend += 1.0)
        {
            var hue = MathUtils.SanitizeDegreesDouble(startHue + directionOfRotation * hueAddend);
            if (!IsBetween(hue, startHue, endHue))
            {
                continue;
            }
            var possibleAnswer = hctsByHue[(int)MathUtils.Round(hue)];
            var relativeTemp = (tempsByHct[possibleAnswer] - coldestTemp) / range;
            var error = Math.Abs(complementRelativeTemp - relativeTemp);
            if (error < smallestError)
            {
                smallestError = error;
                answer = possibleAnswer;
            }
        }
        return answer;
    }
}
