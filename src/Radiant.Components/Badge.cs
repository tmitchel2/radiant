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
            var style = theme.Text(TextType.LabelSmall) with { Color = theme.Get(SurfaceName.Error, on: true) };
            // Sized from its text: placed against the child's corner, it would otherwise be held
            // to the child's width, and "99+" is wider than an icon.
            var width = text is null ? 0f : MathF.Max(16f, MathF.Ceiling(Radiant.Text.Paragraph.Layout(text, style, fonts: context.Root.Fonts).LongestLine) + 8f);
            marker = new Box
            {
                HitTestVisible = false,
                Layout = text is null
                    ? new LayoutStyle { Position = PositionType.Absolute, Width = 6, Height = 6, Inset = new Edges(Dimension.Undefined, 0, 0, Dimension.Undefined) }
                    : new LayoutStyle
                    {
                        Position = PositionType.Absolute,
                        Width = width,
                        Height = 16,
                        Padding = Edges.Symmetric(4, 0),
                        AlignItems = Align.Center,
                        JustifyContent = Justify.Center,
                        Inset = new Edges(Dimension.Undefined, -4, -8, Dimension.Undefined),
                    },
                Background = theme.Get(SurfaceName.Error),
                CornerRadii = Radiant.Graphics2D.CornerRadii.All(8),
                Semantics = text is null ? null : new Semantics { Role = SemanticsRole.None, Label = text },
                Children = text is null ? [] : [new TextBlock(text) { Style = style, Wrap = false }],
            };
        }
        return new Box { Layout = new LayoutStyle { AlignSelf = Align.FlexStart }, Children = [Child, marker] };
    }
}
