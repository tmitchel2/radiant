using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Series as lines across categories (revenue by month), optionally filled down to the axis as
/// areas, on a value axis of round-numbered ticks. The pointer shows every series' value at the
/// category under it; assistive technology reads the values as text.
/// </summary>
/// <param name="Labels">The categories, left to right.</param>
/// <param name="Series">The series, one value per category.</param>
public sealed record LineChart(IReadOnlyList<string> Labels, IReadOnlyList<ChartSeries> Series) : Component
{
    /// <summary>The chart's title.</summary>
    public string? Title { get; init; }

    /// <summary>Whether each line is filled down to the axis.</summary>
    public bool Area { get; init; }

    /// <summary>Whether the value axis starts at zero (otherwise it fits the data).</summary>
    public bool FromZero { get; init; } = true;

    /// <summary>The plot's height.</summary>
    public float Height { get; init; } = 240f;

    /// <summary>How values are written on the axis and in the tooltip.</summary>
    public Func<double, string>? Format { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        var values = Series.SelectMany(s => s.Values).DefaultIfEmpty(0).ToArray();
        var scale = ChartScale.Nice(values.Min(), values.Max(), fromZero: FromZero);
        var (series, count, area) = (Series, Labels.Count, Area);
        return new CartesianFrame(Labels, Series, scale, Bands: false, plot =>
        {
            var baseline = plot.Origin.Y + (float)((1 - scale.Fraction(Math.Clamp(0, scale.Min, scale.Max))) * plot.Size.Y);
            for (var s = 0; s < series.Count; s++)
            {
                var points = new List<Vector2>();
                for (var i = 0; i < Math.Min(count, series[s].Values.Count); i++)
                {
                    points.Add(new Vector2(
                        plot.Origin.X + CartesianFrame.CategoryX(i, count, plot.Size.X, false),
                        plot.Origin.Y + (float)((1 - scale.Fraction(series[s].Values[i])) * plot.Size.Y)));
                }
                var color = plot.Colors[s];
                if (area)
                {
                    // The fill: a band of quads from the line down to the axis, faint enough to show others through.
                    var fill = color with { W = 0.18f };
                    for (var i = 1; i < points.Count; i++)
                    {
                        plot.Renderer.DrawQuad(points[i - 1], points[i], new Vector2(points[i].X, baseline), new Vector2(points[i - 1].X, baseline), fill);
                    }
                }
                plot.Renderer.DrawSmoothPolyline(points, 2.5f, color);
                if (plot.Hover is { } hovered && hovered < points.Count)
                {
                    plot.Renderer.DrawCircleFilled(points[hovered].X, points[hovered].Y, 5f, color);
                }
            }
        })
        {
            Title = Title,
            Height = Height,
            Format = Format,
        };
    }
}
