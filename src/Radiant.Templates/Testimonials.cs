using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>A heading over quotes from customers, each a card with the quote and who said it.</summary>
/// <param name="Title">The heading.</param>
/// <param name="Items">The quotes.</param>
public sealed record Testimonials(string Title, IReadOnlyList<Testimonial> Items) : Component
{
    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var accent = context.UseSurface().With(new SurfaceChange { Content = SurfaceName.Primary });
        var cards = Items.Select(item => (Element?)new Card(
            ThemeContexts.Surface.Provide(accent, new SurfaceIcon("format_quote") { IconSize = 28, IconFilled = true }),
            new SurfaceText(item.Quote) { TextType = TextType.BodyLarge, Layout = new LayoutStyle { FlexGrow = 1 } },
            new Box
            {
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12 },
                Children =
                [
                    new Avatar(item.Name) { Size = 36 },
                    new Box
                    {
                        Children =
                        [
                            new SurfaceText(item.Name) { TextType = TextType.TitleSmall },
                            new SurfaceText(item.Role) { TextType = TextType.BodySmall, Legibility = Legibility.Medium },
                        ],
                    },
                ],
            })
        {
            Layout = new LayoutStyle { RowGap = 16 },
        }).ToList();
        return new Box
        {
            Layout = new LayoutStyle { RowGap = 24, Padding = Edges.Symmetric(0, 16) },
            Children =
            [
                new SurfaceText(Title) { TextType = TextType.HeadlineMedium, HeadingLevel = 2 },
                new Grid { MinColumnWidth = 260, MaxColumns = 3, ColumnGap = 16, RowGap = 16, Children = cards },
            ],
        };
    }
}
