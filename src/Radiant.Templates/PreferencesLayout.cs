using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// A preferences window: a compact list of categories down the left (with section headings from
/// <see cref="NavItem.Section"/>), and the chosen category's settings, under its name, scrolling
/// on the right. With a category focused, Up and Down choose its neighbours.
/// </summary>
/// <param name="Categories">The categories.</param>
/// <param name="Selected">The chosen category's index.</param>
/// <param name="OnSelect">Called with a category's index when it's chosen.</param>
/// <param name="Content">The chosen category's settings (usually <see cref="SettingsSection"/>s).</param>
public sealed record PreferencesLayout(IReadOnlyList<NavItem> Categories, int Selected, Action<int>? OnSelect, Element? Content) : Component
{
    /// <summary>The list's width.</summary>
    public float ListWidth { get; init; } = 220f;

    /// <summary>How wide the settings may grow.</summary>
    public float MaxContentWidth { get; init; } = 760f;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var refs = context.UseMemo(() => Enumerable.Range(0, Categories.Count).Select(_ => new ElementRef()).ToArray(), Categories.Count);
        var select = OnSelect;
        var count = Categories.Count;
        var rows = new List<Element?>();
        string? section = null;
        for (var i = 0; i < Categories.Count; i++)
        {
            var index = i;
            var item = Categories[i];
            if (item.Section is { } heading && heading != section)
            {
                rows.Add(new SurfaceText(heading)
                {
                    TextType = TextType.LabelMedium,
                    Legibility = Legibility.Medium,
                    Layout = new LayoutStyle { Padding = new Edges(12, rows.Count == 0 ? 4 : 16, 12, 4) },
                });
            }
            section = item.Section;
            var chosen = i == Selected;
            rows.Add(new Box
            {
                Ref = refs[i],
                OnKeyDown = e =>
                {
                    var next = index + e.Key switch { KeyCode.Down => 1, KeyCode.Up => -1, _ => 0 };
                    if (next != index && next >= 0 && next < count)
                    {
                        select?.Invoke(next);
                        refs[next].Focus();
                        e.Handled = true;
                    }
                },
                Children =
                [
                    new PressableSurface
                    {
                        InsetFocusRing = true,
                        SurfaceColor = chosen ? SurfaceName.Secondary : null,
                        SurfaceContainerToggle = chosen ? true : null,
                        CornerShape = CornerShapeRole.Small,
                        Role = SemanticsRole.Tab,
                        Label = item.Label,
                        Selected = chosen,
                        OnPress = () => select?.Invoke(index),
                        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 10, Height = 32, Padding = Edges.Symmetric(10, 0) },
                        Children =
                        [
                            new SurfaceIcon(item.Icon) { IconSize = 18, IconFilled = chosen },
                            new SurfaceText(item.Label) { TextType = TextType.LabelLarge, MaxLines = 1 },
                        ],
                    },
                ],
            });
        }
        var title = Selected >= 0 && Selected < Categories.Count ? Categories[Selected].Label : null;
        return new Box
        {
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexGrow = 1 },
            Children =
            [
                new Surface
                {
                    SurfaceColor = SurfaceName.SurfaceContainerLow,
                    Semantics = new Semantics { Role = SemanticsRole.TabList, Label = "Categories" },
                    Layout = new LayoutStyle { Width = ListWidth, Padding = Edges.Symmetric(8, 12), RowGap = 2, FlexShrink = 0 },
                    Children = rows,
                },
                new Box { Layout = new LayoutStyle { Width = 1, AlignSelf = Align.Stretch }, Background = theme.OutlineVariant },
                new Surface
                {
                    SurfaceColor = SurfaceName.Surface,
                    Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                    Children =
                    [
                        new ScrollArea
                        {
                            Layout = new LayoutStyle { FlexGrow = 1 },
                            ContentLayout = new LayoutStyle { Padding = new Edges(32, 24, 32, 32) },
                            Children =
                            [
                                new Box
                                {
                                    Layout = new LayoutStyle { MaxWidth = MaxContentWidth, RowGap = 24 },
                                    Children = [title is null ? null : new SurfaceText(title) { TextType = TextType.HeadlineSmall, HeadingLevel = 1 }, Content],
                                },
                            ],
                        },
                    ],
                },
            ],
        };
    }
}
