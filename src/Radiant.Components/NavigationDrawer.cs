using System;
using System.Collections.Generic;
using System.Linq;
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
public sealed partial record NavigationDrawer(IReadOnlyList<NavItem> Items, int Selected, Action<int>? OnSelect) : Component
{
    [TestId] public static partial string Item { get; }

    /// <summary>A heading at the top.</summary>
    public string? Title { get; init; }

    /// <summary>The drawer's width.</summary>
    public float Width { get; init; } = 280f;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme().Theme;
        var style = theme.Components.Navigation;
        var fillChosen = theme.Components.Icons.FillChosen;
        var rows = new List<Element?>();
        var count = Items.Count;
        // Up and Down move focus through the destinations, as through a tab list; Enter or Space goes there.
        var refs = context.UseMemo(() => Enumerable.Range(0, count).Select(_ => new ElementRef()).ToArray(), count);
        if (Title is not null)
        {
            rows.Add(new SurfaceText(Title) { TextType = style.DrawerSectionText, Legibility = Legibility.Medium, Layout = new LayoutStyle { Padding = new Edges(16, 18, 16, 18) } });
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
                rows.Add(new SurfaceText(section) { TextType = style.DrawerSectionText, Legibility = Legibility.Medium, Layout = new LayoutStyle { Padding = new Edges(16, 12, 16, 12) } });
            }
            rows.Add(new Box
            {
                Ref = refs[i],
                OnKeyDown = e =>
                {
                    var next = e.Key switch
                    {
                        KeyCode.Down => index + 1,
                        KeyCode.Up => index - 1,
                        KeyCode.Home => 0,
                        KeyCode.End => count - 1,
                        _ => -1,
                    };
                    if (next >= 0 && next < count)
                    {
                        refs[next].Focus();
                        e.Handled = true;
                    }
                },
                Children =
                [
                    (chosen ? SurfaceLooks.Pressable(style.DrawerChosen) : new PressableSurface()) with
                    {
                        TestId = Item,
                        InsetFocusRing = true,
                        CornerShape = CornerShapeRole.Control,
                        Role = SemanticsRole.Tab,
                        Selected = chosen,
                        OnPress = () => select?.Invoke(index),
                        Layout = new LayoutStyle
                        {
                            FlexDirection = FlexDirection.Row,
                            AlignItems = Align.Center,
                            Height = style.DrawerItemHeight,
                            Padding = new Edges(style.DrawerItemPadding, 0, style.DrawerItemPadding * 1.5f, 0),
                            ColumnGap = 12,
                        },
                        Children =
                        [
                            new SurfaceIcon(item.Icon) { IconSize = style.DrawerIconSize, IconFilled = chosen && fillChosen, Legibility = chosen ? null : Legibility.Medium },
                            new SurfaceText(item.Label) { TextType = style.DrawerItemText, MaxLines = 1, Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 } },
                            item.Badge is { } badge && badge > 0
                                ? new SurfaceText(badge.ToString(System.Globalization.CultureInfo.InvariantCulture)) { TextType = style.DrawerItemText }
                                : null,
                        ],
                    },
                ],
            });
        }
        // The destinations scroll when there are more than the window's height shows.
        return new Surface
        {
            SurfaceColor = style.DrawerSurface,
            Semantics = new Semantics { Role = SemanticsRole.TabList, Label = Title },
            Layout = new LayoutStyle { Width = Width, AlignSelf = Align.Stretch, FlexDirection = FlexDirection.Row },
            Children =
            [
                new ScrollArea
                {
                    Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                    ContentLayout = new LayoutStyle { Padding = Edges.Symmetric(12, 0) },
                    Children = rows,
                },
                style.DrawerDivider ? new Divider { Vertical = true } : null,
            ],
        };
    }
}
