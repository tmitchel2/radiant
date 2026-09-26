using System;
using System.Collections.Generic;
using Radiant.Theming;

namespace Radiant.Components;

/// <summary>The value axis of a chart: round-numbered ticks covering the data.</summary>
/// <param name="Min">The axis's bottom.</param>
/// <param name="Max">The axis's top.</param>
/// <param name="Step">The gap between ticks.</param>
public readonly record struct ChartScale(double Min, double Max, double Step)
{
    /// <summary>The colour families charts use for series without one, in order.</summary>
    public static IReadOnlyList<SurfaceName> Palette { get; } =
        [SurfaceName.Primary, SurfaceName.Tertiary, SurfaceName.Secondary, SurfaceName.Info, SurfaceName.Success, SurfaceName.Warning, SurfaceName.Error];

    /// <summary>
    /// A scale from <paramref name="min"/> to <paramref name="max"/> with about
    /// <paramref name="ticks"/> ticks at round numbers (1, 2, 2.5 or 5 times a power of ten),
    /// widened to the nearest ticks; zero is included when <paramref name="fromZero"/>.
    /// </summary>
    public static ChartScale Nice(double min, double max, int ticks = 5, bool fromZero = true)
    {
        if (fromZero)
        {
            min = Math.Min(min, 0);
            max = Math.Max(max, 0);
        }
        if (max - min < 1e-12)
        {
            max = min + 1;
        }
        var raw = (max - min) / Math.Max(1, ticks - 1);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var fraction = raw / magnitude;
        var step = (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 2.5 ? 2.5 : fraction <= 5 ? 5 : 10) * magnitude;
        return new ChartScale(Math.Floor(min / step) * step, Math.Ceiling(max / step) * step, step);
    }

    /// <summary>The ticks, bottom to top.</summary>
    public IReadOnlyList<double> Ticks()
    {
        var ticks = new List<double>();
        for (var value = Min; value <= Max + Step * 1e-6; value += Step)
        {
            ticks.Add(Math.Round(value / Step) * Step);
        }
        return ticks;
    }

    /// <summary>Where a value falls, 0 at the bottom to 1 at the top.</summary>
    public double Fraction(double value) => (value - Min) / (Max - Min);
}
