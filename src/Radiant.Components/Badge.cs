using System;
using System.Globalization;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A small marker on the top right of its child: a dot for "something new", or a count (99+ past
/// 99), in the error colour.
/// </summary>
/// <param name="Child">What the badge sits on (usually an icon).</param>
public sealed record Badge(Element? Child) : Component
{
    /// <summary>A number to show; null for a dot.</summary>
    public int? Count { get; init; }

    /// <summary>Whether to show the badge at all.</summary>
    public bool Visible { get; init; } = true;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        Element? marker = null;
        if (Visible)
        {
            var text = Count is { } count ? (count > 99 ? "99+" : count.ToString(CultureInfo.InvariantCulture)) : null;
            marker = new Box
            {
                HitTestVisible = false,
                Layout = text is null
                    ? new LayoutStyle { Position = PositionType.Absolute, Width = 6, Height = 6, Inset = new Edges(Dimension.Undefined, 0, 0, Dimension.Undefined) }
                    : new LayoutStyle
                    {
                        Position = PositionType.Absolute,
                        MinWidth = 16,
                        Height = 16,
                        Padding = Edges.Symmetric(4, 0),
                        AlignItems = Align.Center,
                        JustifyContent = Justify.Center,
                        Inset = new Edges(Dimension.Undefined, -4, -8, Dimension.Undefined),
                    },
                Background = theme.Get(SurfaceName.Error),
                CornerRadii = Radiant.Graphics2D.CornerRadii.All(8),
                Semantics = text is null ? null : new Semantics { Role = SemanticsRole.None, Label = text },
                Children = text is null ? [] : [new TextBlock(text) { Style = theme.Text(TextType.LabelSmall) with { Color = theme.Get(SurfaceName.Error, on: true) }, Wrap = false }],
            };
        }
        return new Box { Layout = new LayoutStyle { AlignSelf = Align.FlexStart }, Children = [Child, marker] };
    }
}
