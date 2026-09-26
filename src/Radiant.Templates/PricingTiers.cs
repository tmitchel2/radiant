using System;
using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>Plans side by side, each with its price, what it includes and a button; the featured plan stands out.</summary>
/// <param name="Tiers">The plans.</param>
/// <param name="OnChoose">Called with a plan's index when its button is pressed.</param>
public sealed partial record PricingTiers(IReadOnlyList<PricingTier> Tiers, Action<int>? OnChoose) : Component
{
    [TestId<SurfaceButton>] public static partial string Choose { get; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        var cards = new List<Element?>();
        for (var i = 0; i < Tiers.Count; i++)
        {
            var index = i;
            var tier = Tiers[i];
            var choose = OnChoose;
            var features = new List<Element?>();
            foreach (var feature in tier.Features)
            {
                features.Add(new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 8 },
                    Children = [new SurfaceIcon("check") { IconSize = 18 }, new SurfaceText(feature)],
                });
            }
            cards.Add(new Card(
                new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, JustifyContent = Justify.SpaceBetween },
                    Children =
                    [
                        new SurfaceText(tier.Name) { TextType = TextType.TitleLarge },
                        tier.Featured ? new Tag("Most popular") { Icon = "star" } : null,
                    ],
                },
                tier.Description is null ? null : new SurfaceText(tier.Description) { Legibility = Legibility.Medium },
                new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Baseline, ColumnGap = 4 },
                    Children = [new SurfaceText(tier.Price) { TextType = TextType.DisplaySmall }, new SurfaceText(tier.Period) { Legibility = Legibility.Medium }],
                },
                new SurfaceButton(tier.Featured ? "Get started" : "Choose plan", tier.Featured ? ButtonVariant.Filled : ButtonVariant.Outlined)
                {
                    TestId = Choose,
                    OnPress = () => choose?.Invoke(index),
                    Layout = new LayoutStyle { AlignSelf = Align.Stretch },
                },
                new Box { Layout = new LayoutStyle { RowGap = 10, Margin = new Edges(0, 8, 0, 0) }, Children = features })
            {
                Variant = tier.Featured ? CardVariant.Elevated : CardVariant.Outlined,
                SurfaceColor = tier.Featured ? SurfaceName.SurfaceContainerHigh : null,
                Layout = new LayoutStyle { Padding = Edges.All(24), RowGap = 12 },
            });
        }
        return new Grid { MinColumnWidth = 240, MaxColumns = 3, ColumnGap = 16, RowGap = 16, Children = cards };
    }
}
