using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>A heading over a grid of features, each an icon in a tinted square, a title and a sentence.</summary>
/// <param name="Title">The heading.</param>
/// <param name="Features">The features.</param>
public sealed record FeatureGrid(string Title, IReadOnlyList<Feature> Features) : Component
{
    /// <summary>A line under the heading.</summary>
    public string? Subtitle { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        var style = context.UseTheme().Theme.Components.Showcase;
        var items = new List<Element?>();
        foreach (var feature in Features)
        {
            items.Add(new Box
            {
                Layout = new LayoutStyle { RowGap = 8 },
                Children =
                [
                    SurfaceLooks.Surface(style.FeatureIcon) with
                    {
                        CornerShape = CornerShapeRole.Medium,
                        Layout = new LayoutStyle { Width = 44, Height = 44, AlignItems = Align.Center, JustifyContent = Justify.Center },
                        Children = [new SurfaceIcon(feature.Icon)],
                    },
                    new SurfaceText(feature.Title) { TextType = TextType.TitleMedium },
                    new SurfaceText(feature.Text) { Legibility = Legibility.Medium },
                ],
            });
        }
        return new Box
        {
            Layout = new LayoutStyle { RowGap = 24, Padding = Edges.Symmetric(0, 16) },
            Children =
            [
                new SurfaceText(Title) { TextType = TextType.HeadlineMedium, HeadingLevel = 2 },
                Subtitle is null ? null : new SurfaceText(Subtitle) { TextType = TextType.BodyLarge, Legibility = Legibility.Medium },
                new Grid { MinColumnWidth = 220, MaxColumns = 3, ColumnGap = 32, RowGap = 32, Children = items },
            ],
        };
    }
}
