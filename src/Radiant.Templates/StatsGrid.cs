using System.Collections.Generic;
using System.Globalization;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>Key figures in cards, side by side, each with its change coloured up (success) or down (error).</summary>
/// <param name="Stats">The figures.</param>
public sealed record StatsGrid(IReadOnlyList<Stat> Stats) : Component
{
    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        var cards = new List<Element?>();
        foreach (var stat in Stats)
        {
            var up = stat.Change >= 0;
            cards.Add(new Card(
                new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8 },
                    Children =
                    [
                        stat.Icon is null ? null : new SurfaceIcon(stat.Icon) { IconSize = 20, Legibility = Legibility.Medium },
                        new SurfaceText(stat.Label) { TextType = TextType.LabelLarge, Legibility = Legibility.Medium },
                    ],
                },
                new SurfaceText(stat.Value) { TextType = TextType.HeadlineMedium },
                stat.Change is not { } change ? null : new Surface
                {
                    ContentColor = up ? SurfaceName.Success : SurfaceName.Error,
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 2 },
                    Children =
                    [
                        new SurfaceIcon(up ? "trending_up" : "trending_down") { IconSize = 18 },
                        new SurfaceText(string.Format(CultureInfo.InvariantCulture, "{0:+0.#%;-0.#%;0%}", change)) { TextType = TextType.LabelLarge },
                    ],
                })
            {
                Variant = CardVariant.Outlined,
                Layout = new LayoutStyle { Padding = Edges.All(20), RowGap = 8 },
            });
        }
        return new Grid { MinColumnWidth = 180, MaxColumns = 4, ColumnGap = 16, RowGap = 16, Children = cards };
    }
}
