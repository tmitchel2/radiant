using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Related fields under a legend ("Shipping address", "Notifications"): outlined, the legend on
/// the outline, and announced as one group named by the legend.
/// </summary>
/// <param name="Legend">What the fields are for.</param>
/// <param name="Children">The fields.</param>
public sealed record Fieldset(string Legend, IReadOnlyList<Element?> Children) : Component
{
    /// <summary>A line under the legend.</summary>
    public string? Description { get; init; }

    /// <summary>The fieldset's own layout, added to its default.</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var surface = context.UseSurface();
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = Legend, Description = Description },
            Layout = new LayoutStyle { Padding = new Edges(0, 10, 0, 0) }.Merge(Layout ?? default),
            Children =
            [
                new Box
                {
                    BorderWidth = 1,
                    BorderColor = theme.OutlineVariant,
                    CornerRadii = theme.Corners(CornerShapeRole.Medium),
                    Layout = new LayoutStyle { RowGap = 12, Padding = new Edges(16, 20, 16, 16) },
                    Children =
                    [
                        Description is null ? null : new SurfaceText(Description) { TextType = TextType.BodySmall, Legibility = Legibility.Medium },
                        .. Children,
                    ],
                },
                // The legend sits on the outline, over a patch of the surface's colour.
                new Box
                {
                    Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(12, 0, Dimension.Undefined, Dimension.Undefined), Padding = Edges.Symmetric(4, 0) },
                    Background = theme.SurfaceColor(surface),
                    Children = [new SurfaceText(Legend) { TextType = TextType.TitleSmall }],
                },
            ],
        };
    }
}
