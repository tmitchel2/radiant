using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Parts of a whole as a ring (traffic by source), each slice with a small gap, the total or a
/// caption in the middle, and a legend with each part's share. Assistive technology reads the
/// parts and shares as text.
/// </summary>
/// <param name="Slices">The parts: a label and a value each.</param>
public sealed record DonutChart(IReadOnlyList<(string Label, double Value)> Slices) : Component
{
    /// <summary>The chart's title.</summary>
    public string? Title { get; init; }

    /// <summary>What the middle says; the total if null.</summary>
    public string? Caption { get; init; }

    /// <summary>The ring's outer diameter.</summary>
    public float Size { get; init; } = 180f;

    /// <summary>How values are written.</summary>
    public Func<double, string>? Format { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var format = Format ?? (v => v.ToString("#,0.##", CultureInfo.CurrentCulture));
        var total = Slices.Sum(s => Math.Max(0, s.Value));
        var colors = Slices.Select((_, i) => (Vector4)theme.Get(ChartScale.Palette[i % ChartScale.Palette.Count])).ToArray();
        var track = (Vector4)theme.Get(SurfaceName.SurfaceContainerHighest);
        var (slices, size) = (Slices, Size);
        const float thickness = 22f;
        string Share(double value) => total <= 0 ? "0%" : (value / total).ToString("0%", CultureInfo.CurrentCulture);

        var ring = new Canvas((paint, origin, box) =>
        {
            var center = origin + box / 2;
            var radius = MathF.Min(box.X, box.Y) / 2 - thickness / 2;
            if (total <= 0)
            {
                paint.Renderer.DrawArc(center, radius, thickness, 0, MathF.Tau, track);
                return;
            }
            // Round ends reach half the thickness past a slice's angles: leave that, and a gap, between slices.
            var gap = (thickness + 4) / radius;
            var angle = -MathF.PI / 2;
            for (var i = 0; i < slices.Count; i++)
            {
                var sweep = (float)(Math.Max(0, slices[i].Value) / total * MathF.Tau);
                var drawn = slices.Count == 1 ? MathF.Tau : sweep - gap;
                if (drawn > 0.001f)
                {
                    paint.Renderer.DrawArc(center, radius, thickness, angle + (sweep - drawn) / 2, drawn, colors[i]);
                }
                else if (sweep > 0)
                {
                    // Too small for a slice with a gap: a dot at its middle.
                    var middle = angle + sweep / 2;
                    paint.Renderer.DrawCircleFilled(center.X + radius * MathF.Cos(middle), center.Y + radius * MathF.Sin(middle), thickness / 2, colors[i]);
                }
                angle += sweep;
            }
        })
        {
            Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = Edges.All(0) },
        };

        var summary = $"{Title ?? "Chart"}: " + string.Join("; ", slices.Select(s => $"{s.Label} {format(s.Value)} ({Share(s.Value)})"));
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Image, Label = Title ?? "Chart", Description = summary },
            Layout = new LayoutStyle { RowGap = 12 },
            Children =
            [
                Title is null ? null : new SurfaceText(Title) { TextType = TextType.TitleMedium },
                new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 24, FlexWrap = FlexWrap.Wrap, RowGap = 12 },
                    Children =
                    [
                        new Box
                        {
                            Layout = new LayoutStyle { Width = size, Height = size, AlignItems = Align.Center, JustifyContent = Justify.Center, FlexShrink = 0 },
                            Children =
                            [
                                ring,
                                new SurfaceText(Caption ?? format(total)) { TextType = TextType.HeadlineSmall },
                            ],
                        },
                        new Box
                        {
                            Layout = new LayoutStyle { RowGap = 8 },
                            Children =
                            [
                                .. slices.Select((s, i) => (Element?)new Box
                                {
                                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8 },
                                    Children =
                                    [
                                        new Box { Layout = new LayoutStyle { Width = 10, Height = 10 }, Background = colors[i], CornerRadii = Radiant.Graphics2D.CornerRadii.All(5) },
                                        new SurfaceText(s.Label) { TextType = TextType.BodyMedium, Layout = new LayoutStyle { MinWidth = 90 } },
                                        new SurfaceText(Share(s.Value)) { TextType = TextType.LabelLarge, Legibility = Legibility.Medium },
                                    ],
                                }),
                            ],
                        },
                    ],
                },
            ],
        };
    }
}
