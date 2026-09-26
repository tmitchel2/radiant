using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Layout;
using Radiant.Scrolling;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// An editor's strip of open documents. The chosen tab takes the page's surface colour, so it
/// joins the document below it, with a line in the primary colour along its top. A tab closes
/// from its close button (shown on the chosen or hovered tab) or a middle click, and shows a dot
/// in place of the close button when it has unsaved changes. With a tab focused, Left and Right
/// choose its neighbours. Tabs that don't fit scroll sideways.
/// </summary>
/// <param name="Tabs">The open documents.</param>
/// <param name="Selected">The chosen tab's index.</param>
/// <param name="OnSelect">Called with a tab's index when it's chosen.</param>
public sealed record DocumentTabs(IReadOnlyList<DocumentTab> Tabs, int Selected, Action<int>? OnSelect) : Component
{
    /// <summary>Called with a tab's index when it's closed.</summary>
    public Action<int>? OnClose { get; init; }

    /// <summary>Buttons after the tabs (a new-document button, a menu).</summary>
    public IReadOnlyList<Element?> Actions { get; init; } = [];

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var refs = context.UseMemo(() => Enumerable.Range(0, Tabs.Count).Select(_ => new ElementRef()).ToArray(), Tabs.Count);
        var select = OnSelect;
        var close = OnClose;
        var count = Tabs.Count;
        var tabs = new List<Element?>();
        for (var i = 0; i < Tabs.Count; i++)
        {
            var index = i;
            var tab = Tabs[i];
            tabs.Add(new TabView(tab, i == Selected, refs[i], () => select?.Invoke(index), tab.Closable && close is not null ? () => close(index) : null)
            {
                Key = tab.Id ?? tab.Label,
                OnArrow = step =>
                {
                    var next = index + step;
                    if (next >= 0 && next < count)
                    {
                        select?.Invoke(next);
                        refs[next].Focus();
                    }
                },
            });
        }
        return new Surface
        {
            SurfaceColor = SurfaceName.SurfaceContainer,
            Semantics = new Semantics { Role = SemanticsRole.TabList },
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Stretch, Height = 36 + theme.DensityOffset, FlexShrink = 0 },
            Children =
            [
                new ScrollArea
                {
                    Behaviour = new ScrollBehaviour { Axes = ScrollAxes.Horizontal },
                    IndicatorColor = default,
                    Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                    ContentLayout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Stretch },
                    Children = tabs,
                },
                Actions.Count == 0 ? null : new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, Padding = Edges.Symmetric(4, 0) },
                    Children = Actions,
                },
            ],
        };
    }

    /// <summary>One tab: its icon, name, and the close button or modified dot.</summary>
    private sealed record TabView(DocumentTab Tab, bool Chosen, ElementRef Ref, Action Select, Action? Close) : Component
    {
        public Action<int>? OnArrow { get; init; }

        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var hovered = context.UseState(false);
            var close = Close;
            var arrow = OnArrow;
            var rightToLeft = context.UseRightToLeft();
            var showClose = close is not null && (Chosen || hovered.Value);
            Element? trailing = showClose
                ? new CloseButton(Tab.Label, close!)
                : Tab.Modified
                    ? new SurfaceIcon("circle") { IconSize = 10, IconFilled = true, Legibility = Legibility.Medium }
                    : null;
            return new Box
            {
                Ref = Ref,
                Layout = new LayoutStyle { AlignItems = Align.Stretch },
                OnPointerEnter = _ => hovered.Set(true),
                OnPointerLeave = _ => hovered.Set(false),
                OnPointerDown = e =>
                {
                    if (e.Button == PointerButton.Middle && close is not null)
                    {
                        close();
                        e.Handled = true;
                    }
                },
                OnKeyDown = e =>
                {
                    var step = e.Key.ForDirection(rightToLeft) switch { KeyCode.Right => 1, KeyCode.Left => -1, _ => 0 };
                    if (step != 0)
                    {
                        arrow?.Invoke(step);
                        e.Handled = true;
                    }
                },
                Children =
                [
                    new PressableSurface
                    {
                        SurfaceColor = Chosen ? SurfaceName.Surface : null,
                        Role = SemanticsRole.Tab,
                        Label = Tab.Label,
                        Selected = Chosen,
                        OnPress = Select,
                        Layout = new LayoutStyle
                        {
                            FlexGrow = 1,
                            FlexDirection = FlexDirection.Row,
                            AlignItems = Align.Center,
                            ColumnGap = 6,
                            MaxWidth = 240,
                            Padding = new Edges(12, 0, 6, 0),
                        },
                        Children =
                        [
                            Tab.Icon is null ? null : new SurfaceIcon(Tab.Icon) { IconSize = 16, Legibility = Chosen ? null : Legibility.Medium },
                            new SurfaceText(Tab.Label)
                            {
                                TextType = TextType.LabelLarge,
                                MaxLines = 1,
                                Legibility = Chosen ? null : Legibility.Medium,
                                Layout = new LayoutStyle { FlexShrink = 1 },
                            },
                            // The trailing slot keeps its width, so tabs don't jump as the close button comes and goes.
                            new Box
                            {
                                Layout = new LayoutStyle { Width = 20, Height = 20, AlignItems = Align.Center, JustifyContent = Justify.Center, FlexShrink = 0 },
                                Children = [trailing],
                            },
                            Chosen ? new Box
                            {
                                HitTestVisible = false,
                                Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(0, 0, 0, Dimension.Undefined), Height = 2 },
                                Background = theme.Get(SurfaceName.Primary),
                            } : null,
                        ],
                    },
                    new Box
                    {
                        HitTestVisible = false,
                        Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(Dimension.Undefined, 8, 0, 8), Width = 1 },
                        Background = theme.OutlineVariant,
                    },
                ],
            };
        }
    }

    /// <summary>A tab's small close button; it isn't a tab stop (keyboard users close from a command).</summary>
    private sealed record CloseButton(string Label, Action Close) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var surface = context.UseSurface();
            var hovered = context.UseState(false);
            var close = Close;
            return new Box
            {
                Semantics = new Semantics { Role = SemanticsRole.Button, Label = $"Close {Label}" },
                Layout = new LayoutStyle { Width = 20, Height = 20, AlignItems = Align.Center, JustifyContent = Justify.Center },
                CornerRadii = Radiant.Graphics2D.CornerRadii.All(4),
                Background = hovered.Value ? theme.StateLayerColor(surface, theme.Theme.StateLayers.Hover * 1.5f) : null,
                OnPointerEnter = _ => hovered.Set(true),
                OnPointerLeave = _ => hovered.Set(false),
                OnClick = e =>
                {
                    if (e.Button == PointerButton.Left)
                    {
                        close();
                        e.Handled = true;
                    }
                },
                Children = [new SurfaceIcon("close") { IconSize = 14 }],
            };
        }
    }
}
