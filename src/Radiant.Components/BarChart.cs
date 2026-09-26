using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Graphics2D;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Series as bars per category (orders by region): side by side, or stacked into one bar with
/// <see cref="Stacked"/>. The pointer shows every series' value at the category under it;
/// assistive technology reads the values as text.
/// </summary>
/// <param name="Labels">The categories, left to right.</param>
/// <param name="Series">The series, one value per category.</param>
public sealed record BarChart(IReadOnlyList<string> Labels, IReadOnlyList<ChartSeries> Series) : Component
{
    /// <summary>The chart's title.</summary>
    public string? Title { get; init; }

    /// <summary>Whether a category's series stack into one bar.</summary>
    public bool Stacked { get; init; }

    /// <summary>The plot's height.</summary>
    public float Height { get; init; } = 240f;

    /// <summary>How values are written on the axis and in the tooltip.</summary>
    public Func<double, string>? Format { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        var count = Labels.Count;
        double Total(int i) => Series.Sum(s => i < s.Values.Count ? Math.Max(0, s.Values[i]) : 0);
        var extremes = Stacked
            ? Enumerable.Range(0, count).Select(Total).DefaultIfEmpty(0).ToArray()
            : Series.SelectMany(s => s.Values).DefaultIfEmpty(0).ToArray();
        var scale = ChartScale.Nice(extremes.Min(), extremes.Max());
        var (series, stacked) = (Series, Stacked);
        return new CartesianFrame(Labels, Series, scale, Bands: true, plot =>
        {
            var band = plot.Size.X / Math.Max(1, count);
            var y0 = plot.Origin.Y + (float)((1 - scale.Fraction(0)) * plot.Size.Y);
            float Y(double value) => plot.Origin.Y + (float)((1 - scale.Fraction(value)) * plot.Size.Y);
            for (var i = 0; i < count; i++)
            {
                var left = plot.Origin.X + i * band + band * 0.15f;
                var width = band * 0.7f;
                var hovered = plot.Hover == i;
                if (stacked)
                {
                    // Bottom to top in series order; only the top segment is rounded.
                    var below = 0.0;
                    for (var s = 0; s < series.Count; s++)
                    {
                        var value = i < series[s].Values.Count ? Math.Max(0, series[s].Values[i]) : 0;
                        if (value <= 0)
                        {
                            continue;
                        }
                        var top = Y(below + value);
                        var bottom = Y(below);
                        var last = series.Skip(s + 1).All(o => i >= o.Values.Count || o.Values[i] <= 0);
                        plot.Renderer.DrawRoundedRectFilled(left, top, width, bottom - top, last ? CornerRadii.Top(4) : default, Shade(plot.Colors[s], hovered));
                        below += value;
                    }
                }
                else
                {
                    var each = width / Math.Max(1, series.Count);
                    for (var s = 0; s < series.Count; s++)
                    {
                        if (i >= series[s].Values.Count)
                        {
                            continue;
                        }
                        var value = series[s].Values[i];
                        var y = Y(value);
                        var (top, height) = value >= 0 ? (y, y0 - y) : (y0, y - y0);
                        var radii = value >= 0 ? CornerRadii.Top(4) : new CornerRadii(0, 0, 4, 4);
                        plot.Renderer.DrawRoundedRectFilled(left + s * each + 1, top, each - 2, height, radii, Shade(plot.Colors[s], hovered));
                    }
                }
            }
        })
        {
            Title = Title,
            Height = Height,
            Format = Format,
        };

        // The hovered category's bars darken a little, as a state layer would.
        static System.Numerics.Vector4 Shade(System.Numerics.Vector4 color, bool hovered) =>
            hovered ? new System.Numerics.Vector4(color.X * 0.85f, color.Y * 0.85f, color.Z * 0.85f, color.W) : color;
    }
}
