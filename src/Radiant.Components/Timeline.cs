using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Events in order down a line (an order's progress, an activity feed): a marker for each, joined
/// to the next, with what happened, when, and more detail.
/// </summary>
/// <param name="Events">The events, first to last.</param>
public sealed record Timeline(IReadOnlyList<TimelineEvent> Events) : Component
{
    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var rows = new List<Element?>();
        for (var i = 0; i < Events.Count; i++)
        {
            var item = Events[i];
            var last = i == Events.Count - 1;
            rows.Add(new Box
            {
                Semantics = new Semantics { Role = SemanticsRole.ListItem, Label = item.Title, Description = item.Text, Value = item.Time },
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = 16 },
                Children =
                [
                    // The marker, and the line down to the next one.
                    new Box
                    {
                        Layout = new LayoutStyle { Width = 28, AlignItems = Align.Center },
                        Children =
                        [
                            new Surface
                            {
                                SurfaceColor = item.Color ?? SurfaceName.Primary,
                                SurfaceContainerToggle = item.Icon is null ? null : true,
                                CornerShape = CornerShapeRole.Full,
                                Layout = item.Icon is null
                                    ? new LayoutStyle { Width = 12, Height = 12, Margin = new Edges(0, 6, 0, 6) }
                                    : new LayoutStyle { Width = 28, Height = 28, AlignItems = Align.Center, JustifyContent = Justify.Center },
                                Children = [item.Icon is null ? null : new SurfaceIcon(item.Icon) { IconSize = 16 }],
                            },
                            last ? null : new Box
                            {
                                Layout = new LayoutStyle { Width = 2, FlexGrow = 1, MinHeight = 16, Margin = new Edges(0, 4, 0, 4) },
                                Background = theme.OutlineVariant,
                            },
                        ],
                    },
                    new Box
                    {
                        Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, RowGap = 2, Padding = new Edges(0, item.Icon is null ? 2 : 4, 0, last ? 0 : 20) },
                        Children =
                        [
                            new Box
                            {
                                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = 12 },
                                Children =
                                [
                                    new SurfaceText(item.Title) { TextType = TextType.TitleSmall, Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 } },
                                    new SurfaceText(item.Time) { TextType = TextType.LabelMedium, Legibility = Legibility.Medium },
                                ],
                            },
                            item.Text is null ? null : new SurfaceText(item.Text) { Legibility = Legibility.Medium },
                        ],
                    },
                ],
            });
        }
        return new Box { Semantics = new Semantics { Role = SemanticsRole.List }, Children = rows };
    }
}
