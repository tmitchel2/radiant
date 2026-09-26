using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A prominent message across the top of a page or pane that stays until it's dealt with ("You're
/// offline", "Your trial ends in 3 days"): an icon, the message, and actions at the end. Unlike a
/// snackbar it doesn't time out; unlike an alert it spans the width it's given.
/// </summary>
/// <param name="Text">The message.</param>
public sealed record Banner(string Text) : Component
{
    /// <summary>An icon before the message, in a tinted circle.</summary>
    public string? Icon { get; init; }

    /// <summary>Actions at the end (text buttons, usually one or two).</summary>
    public IReadOnlyList<Element?> Actions { get; init; } = [];

    /// <summary>The colour family the icon is tinted with; primary by default.</summary>
    public SurfaceName IconColor { get; init; } = SurfaceName.Primary;

    /// <summary>The banner's own layout, added to its default (stretching across its parent).</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        return new Surface
        {
            SurfaceColor = SurfaceName.SurfaceContainerLow,
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = Text },
            Layout = new LayoutStyle { AlignSelf = Align.Stretch }.Merge(Layout ?? default),
            Children =
            [
                new Box
                {
                    Layout = new LayoutStyle
                    {
                        FlexDirection = FlexDirection.Row,
                        FlexWrap = FlexWrap.Wrap,
                        AlignItems = Align.Center,
                        ColumnGap = 16,
                        RowGap = 8,
                        Padding = new Edges(16, 12, 8, 12),
                    },
                    Children =
                    [
                        Icon is null ? null : new Surface
                        {
                            SurfaceColor = IconColor,
                            SurfaceContainerToggle = true,
                            CornerShape = CornerShapeRole.Full,
                            Layout = new LayoutStyle { Width = 40, Height = 40, AlignItems = Align.Center, JustifyContent = Justify.Center, FlexShrink = 0 },
                            Children = [new SurfaceIcon(Icon)],
                        },
                        new SurfaceText(Text) { TextType = TextType.BodyMedium, Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, FlexBasis = 200 } },
                        Actions.Count == 0 ? null : new Box
                        {
                            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = 8, Margin = new Edges(Dimension.Auto, 0, 0, 0) },
                            Children = Actions,
                        },
                    ],
                },
                new Box { Layout = new LayoutStyle { Height = 1 }, Background = theme.OutlineVariant },
            ],
        };
    }
}
