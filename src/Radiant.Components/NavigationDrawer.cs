using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A sidebar of destinations: a heading, sections, and rows with an icon, label and count, the
/// current one in a filled pill. The standing sidebar of a desktop app.
/// </summary>
/// <param name="Items">The destinations.</param>
/// <param name="Selected">The current destination's index.</param>
/// <param name="OnSelect">Called with a destination's index when it's chosen.</param>
public sealed record NavigationDrawer(IReadOnlyList<NavItem> Items, int Selected, Action<int>? OnSelect) : Component
{
    /// <summary>A heading at the top.</summary>
    public string? Title { get; init; }

    /// <summary>The drawer's width.</summary>
    public float Width { get; init; } = 280f;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rows = new List<Element?>();
        if (Title is not null)
        {
            rows.Add(new SurfaceText(Title) { TextType = TextType.TitleSmall, Legibility = Legibility.Medium, Layout = new LayoutStyle { Padding = new Edges(16, 18, 16, 18) } });
        }
        for (var i = 0; i < Items.Count; i++)
        {
            var index = i;
            var item = Items[i];
            var chosen = i == Selected;
            var select = OnSelect;
            if (item.Section is { } section)
            {
                if (i > 0)
                {
                    rows.Add(new Box { Layout = new LayoutStyle { Padding = Edges.Symmetric(16, 8) }, Children = [new Divider()] });
                }
                rows.Add(new SurfaceText(section) { TextType = TextType.TitleSmall, Legibility = Legibility.Medium, Layout = new LayoutStyle { Padding = new Edges(16, 12, 16, 12) } });
            }
            rows.Add(new PressableSurface
            {
                InsetFocusRing = true,
                SurfaceColor = chosen ? SurfaceName.Secondary : null,
                SurfaceContainerToggle = chosen ? true : null,
                CornerShape = CornerShapeRole.Full,
                Role = SemanticsRole.Tab,
                Selected = chosen,
                OnPress = () => select?.Invoke(index),
                Layout = new LayoutStyle
                {
                    FlexDirection = FlexDirection.Row,
                    AlignItems = Align.Center,
                    Height = 56,
                    Padding = new Edges(16, 0, 24, 0),
                    ColumnGap = 12,
                },
                Children =
                [
                    new SurfaceIcon(item.Icon) { IconFilled = chosen, Legibility = chosen ? null : Legibility.Medium },
                    new SurfaceText(item.Label) { TextType = TextType.LabelLarge, MaxLines = 1, Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 } },
                    item.Badge is { } badge && badge > 0
                        ? new SurfaceText(badge.ToString(System.Globalization.CultureInfo.InvariantCulture)) { TextType = TextType.LabelLarge }
                        : null,
                ],
            });
        }
        // The destinations scroll when there are more than the window's height shows.
        return new Surface
        {
            SurfaceColor = SurfaceName.SurfaceContainerLow,
            Semantics = new Semantics { Role = SemanticsRole.TabList, Label = Title },
            Layout = new LayoutStyle { Width = Width, AlignSelf = Align.Stretch },
            Children =
            [
                new ScrollArea
                {
                    Layout = new LayoutStyle { FlexGrow = 1 },
                    ContentLayout = new LayoutStyle { Padding = Edges.Symmetric(12, 0) },
                    Children = rows,
                },
            ],
        };
    }
}
