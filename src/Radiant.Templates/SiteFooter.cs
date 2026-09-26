using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// A site's footer: the name and a line about it, columns of links that wrap on narrow widths,
/// and a copyright line under a divider.
/// </summary>
/// <param name="Name">The product or company name.</param>
/// <param name="Columns">The link columns.</param>
public sealed record SiteFooter(string Name, IReadOnlyList<FooterColumn> Columns) : Component
{
    /// <summary>A line under the name.</summary>
    public string? Tagline { get; init; }

    /// <summary>The copyright line ("© 2026 Radiant").</summary>
    public string? Copyright { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var columns = Columns.Select(column => (Element?)new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = column.Title },
            Layout = new LayoutStyle { RowGap = 10, MinWidth = 140 },
            Children =
            [
                new SurfaceText(column.Title) { TextType = TextType.TitleSmall },
                .. column.Links.Select(text => (Element?)new Link(text, column.OnFollow is { } follow ? () => follow(text) : null)),
            ],
        }).ToList();
        return new Surface
        {
            SurfaceColor = SurfaceName.SurfaceContainerLow,
            Layout = new LayoutStyle { AlignSelf = Align.Stretch, Padding = Edges.Symmetric(24, 32), RowGap = 24 },
            Children =
            [
                new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexWrap = FlexWrap.Wrap, ColumnGap = 48, RowGap = 24 },
                    Children =
                    [
                        new Box
                        {
                            Layout = new LayoutStyle { RowGap = 8, FlexBasis = 220, FlexGrow = 1 },
                            Children =
                            [
                                new SurfaceText(Name) { TextType = TextType.TitleLarge },
                                Tagline is null ? null : new SurfaceText(Tagline) { Legibility = Legibility.Medium, Layout = new LayoutStyle { MaxWidth = 280 } },
                            ],
                        },
                        .. columns,
                    ],
                },
                Copyright is null ? null : new Divider(),
                Copyright is null ? null : new SurfaceText(Copyright) { TextType = TextType.BodySmall, Legibility = Legibility.Medium },
            ],
        };
    }
}
