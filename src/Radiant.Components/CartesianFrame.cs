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
/// What line and bar charts share: a title, the value axis with its ticks, the plot with grid lines,
/// category labels along the bottom, a legend, and a tooltip of every series' value at the category
/// under the pointer. The chart paints the plot itself.
/// </summary>
internal sealed record CartesianFrame(
    IReadOnlyList<string> Labels,
    IReadOnlyList<ChartSeries> Series,
    ChartScale Scale,
    bool Bands,
    Action<ChartPlot> PaintPlot) : Component
{
    private const float AxisWidth = 48f;

    public string? Title { get; init; }

    public float Height { get; init; } = 240f;

    public Func<double, string>? Format { get; init; }

    /// <summary>Where category <paramref name="index"/>'s centre is across a plot <paramref name="width"/> wide.</summary>
    public static float CategoryX(int index, int count, float width, bool bands) =>
        bands ? (index + 0.5f) * width / Math.Max(1, count)
        : count <= 1 ? width / 2 : index * width / (count - 1);

    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var surface = context.UseSurface();
        var hover = context.UseState((int?)null);
        var plotRef = context.UseRef(new ElementRef()).Value;
        // The plot's laid-out width places the category labels and tooltip; it's known after layout.
        var plotWidth = context.UseState(0f);
        context.UseEffect(() =>
        {
            if (plotRef.IsMounted && plotRef.Bounds.Width != plotWidth.Value)
            {
                plotWidth.Set(plotRef.Bounds.Width);
            }
            return null;
        });
        var format = Format ?? (v => v.ToString("#,0.##", CultureInfo.CurrentCulture));
        var colors = Series.Select((s, i) => (Vector4)theme.Get(s.Color ?? ChartScale.Palette[i % ChartScale.Palette.Count])).ToArray();
        var grid = (Vector4)theme.OutlineVariant;
        var (labels, series, scale, bands, paint, height) = (Labels, Series, Scale, Bands, PaintPlot, Height);
        var count = labels.Count;
        var hovered = hover.Value;

        // The value axis: tick labels right-aligned beside the grid lines they name.
        var ticks = scale.Ticks();
        var tickLabels = ticks.Select(t => (Element?)new SurfaceText(format(t))
        {
            TextType = TextType.LabelSmall,
            Legibility = Legibility.Medium,
            Alignment = Radiant.Text.TextAlignment.End,
            Layout = new LayoutStyle
            {
                Position = PositionType.Absolute,
                Inset = new Edges(0, (float)((1 - scale.Fraction(t)) * height) - 8, 8, Dimension.Undefined),
                Width = AxisWidth - 8,
            },
        }).ToArray();

        var plot = new Box
        {
            Ref = plotRef,
            Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, Height = height },
            OnPointerMove = e =>
            {
                var width = plotRef.Bounds.Width;
                if (width <= 0 || count == 0)
                {
                    return;
                }
                var x = e.Position.X - plotRef.Bounds.X;
                var index = bands ? (int)(x / width * count) : (int)MathF.Round(x / width * (count - 1));
                index = Math.Clamp(index, 0, count - 1);
                if (hover.Value != index)
                {
                    hover.Set(index);
                }
            },
            OnPointerLeave = _ => hover.Set(null),
            Children =
            [
                new Canvas((paintContext, origin, size) =>
                {
                    var renderer = paintContext.Renderer;
                    foreach (var tick in ticks)
                    {
                        var y = origin.Y + (float)((1 - scale.Fraction(tick)) * size.Y);
                        renderer.DrawRectangleFilled(origin.X, MathF.Round(y), size.X, 1, grid);
                    }
                    paint(new ChartPlot(renderer, origin, size, colors, hovered));
                    if (hovered is { } index)
                    {
                        var x = origin.X + CategoryX(index, count, size.X, bands);
                        renderer.DrawRectangleFilled(MathF.Round(x), origin.Y, 1, size.Y, grid with { W = grid.W * 2 });
                    }
                })
                {
                    Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = Edges.All(0) },
                },
                hovered is not { } at ? null : Tooltip(at, plotWidth.Value),
            ],
        };

        // Category labels, centred under their points or bands; crowded axes show every nth.
        var every = Math.Max(1, (int)Math.Ceiling(count / 12.0));
        var categoryLabels = new List<Element?>();
        for (var i = 0; i < count; i += every)
        {
            var x = CategoryX(i, count, plotWidth.Value, bands);
            categoryLabels.Add(new SurfaceText(labels[i])
            {
                TextType = TextType.LabelSmall,
                Legibility = Legibility.Medium,
                MaxLines = 1,
                Alignment = Radiant.Text.TextAlignment.Center,
                Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(x - 40, 0, Dimension.Undefined, Dimension.Undefined), Width = 80 },
            });
        }

        var summary = $"{Title ?? "Chart"}: " + string.Join("; ", series.Select(s => $"{s.Name} {string.Join(", ", s.Values.Select(format))}"));
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Image, Label = Title ?? "Chart", Description = summary },
            // A chart's x axis runs left to right in any language; its labels still read the UI's way.
            Layout = new LayoutStyle { AlignSelf = Align.Stretch, RowGap = 8, Direction = Radiant.Text.TextDirection.LeftToRight },
            Children =
            [
                Title is null ? null : new SurfaceText(Title) { TextType = TextType.TitleMedium },
                new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, Margin = new Edges(0, 8, 0, 0) },
                    Children =
                    [
                        new Box { Layout = new LayoutStyle { Width = AxisWidth, Height = height, FlexShrink = 0 }, Children = tickLabels },
                        plot,
                    ],
                },
                new Box { Layout = new LayoutStyle { Height = 18, Margin = new Edges(AxisWidth, 0, 0, 0) }, Children = categoryLabels },
                series.Count < 2 && Title is not null ? null : new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexWrap = FlexWrap.Wrap, ColumnGap = 16, RowGap = 4, Margin = new Edges(AxisWidth, 4, 0, 0) },
                    Children = [.. series.Select((s, i) => (Element?)Swatch(s.Name, colors[i]))],
                },
            ],
        };

        Element Swatch(string name, Vector4 color) => new Box
        {
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 6 },
            Children =
            [
                new Box { Layout = new LayoutStyle { Width = 10, Height = 10 }, Background = color, CornerRadii = Radiant.Graphics2D.CornerRadii.All(3) },
                new SurfaceText(name) { TextType = TextType.LabelMedium, Legibility = Legibility.Medium },
            ],
        };

        Element Tooltip(int index, float width)
        {
            var x = CategoryX(index, count, width, bands);
            // Beside the crosshair, on whichever side has room.
            var right = x < width - 180;
            return new Surface
            {
                SurfaceColor = SurfaceName.Inverse,
                CornerShape = CornerShapeRole.ExtraSmall,
                Layout = new LayoutStyle
                {
                    Position = PositionType.Absolute,
                    Inset = right ? new Edges(x + 12, 8, Dimension.Undefined, Dimension.Undefined) : new Edges(Dimension.Undefined, 8, width - x + 12, Dimension.Undefined),
                    Padding = Edges.Symmetric(10, 8),
                    RowGap = 4,
                },
                Children =
                [
                    new SurfaceText(labels[index]) { TextType = TextType.LabelMedium },
                    .. series.Select((s, i) => (Element?)new Box
                    {
                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 6 },
                        Children =
                        [
                            new Box { Layout = new LayoutStyle { Width = 8, Height = 8 }, Background = colors[i], CornerRadii = Radiant.Graphics2D.CornerRadii.All(4) },
                            new SurfaceText($"{s.Name}  {(index < s.Values.Count ? format(s.Values[index]) : "–")}") { TextType = TextType.BodySmall },
                        ],
                    }),
                ],
            };
        }
    }
}
