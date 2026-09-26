using System.Collections.Generic;
using Radiant.Theming;

namespace Radiant.Components;

/// <summary>A named run of values for a chart, one per category.</summary>
/// <param name="Name">What it measures, for the legend and tooltip.</param>
/// <param name="Values">A value per category, in order.</param>
public sealed record ChartSeries(string Name, IReadOnlyList<double> Values)
{
    /// <summary>Its colour family; the chart picks one from its palette if null.</summary>
    public SurfaceName? Color { get; init; }
}
