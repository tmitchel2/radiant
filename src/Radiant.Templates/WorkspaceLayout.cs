using System;
using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// An IDE-style docked workspace: an activity bar down the left edge choosing what the side bar
/// shows, the side bar, the editor with an optional panel under it (a terminal, problems) and an
/// optional inspector on the right, all divided by splitters, and a status bar along the bottom.
/// Leave out a part (null) and its splitter goes with it.
/// </summary>
/// <param name="Activities">The activity bar's choices (explorer, search, source control).</param>
/// <param name="Activity">The chosen activity, or -1 to hide the side bar.</param>
/// <param name="OnActivity">Called with an activity's index when it's pressed.</param>
/// <param name="Editor">The main area (usually document tabs over the document).</param>
public sealed record WorkspaceLayout(IReadOnlyList<NavItem> Activities, int Activity, Action<int>? OnActivity, Element? Editor) : Component
{
    /// <summary>The side bar's content, for the chosen activity.</summary>
    public Element? Sidebar { get; init; }

    /// <summary>Buttons in the side bar's header.</summary>
    public IReadOnlyList<Element?> SidebarActions { get; init; } = [];

    /// <summary>The panel under the editor, or null for none.</summary>
    public Element? Panel { get; init; }

    /// <summary>The panel's title.</summary>
    public string? PanelTitle { get; init; }

    /// <summary>The inspector to the editor's right, or null for none.</summary>
    public Element? Inspector { get; init; }

    /// <summary>The status bar, or null for none.</summary>
    public Element? StatusBar { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        var showSidebar = Activity >= 0 && Activity < Activities.Count && Sidebar is not null;
        Element? editor = new Surface { SurfaceColor = SurfaceName.Surface, Layout = new LayoutStyle { FlexGrow = 1 }, Children = [Editor] };
        if (Panel is not null)
        {
            editor = new Splitter(editor, Pane(PanelTitle, [], Panel, SurfaceName.Surface))
            {
                Orientation = Orientation.Vertical,
                SizedPane = SplitterPane.Second,
                InitialSize = 200,
                MinSize = 80,
                Label = PanelTitle ?? "Panel",
            };
        }
        if (Inspector is not null)
        {
            editor = new Splitter(editor, new Surface { SurfaceColor = SurfaceName.SurfaceContainerLow, Layout = new LayoutStyle { FlexGrow = 1 }, Children = [Inspector] })
            {
                SizedPane = SplitterPane.Second,
                InitialSize = 280,
                MinSize = 200,
                MinOtherSize = 240,
                Label = "Inspector",
            };
        }
        if (showSidebar)
        {
            editor = new Splitter(Pane(Activities[Activity].Label, SidebarActions, Sidebar, SurfaceName.SurfaceContainerLow), editor)
            {
                InitialSize = 260,
                MinSize = 160,
                MinOtherSize = 240,
                Label = "Side bar",
            };
        }
        return new Surface
        {
            SurfaceColor = SurfaceName.Surface,
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children =
            [
                new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexGrow = 1, FlexShrink = 1 },
                    Children = [new ActivityBar(Activities, Activity, OnActivity), editor],
                },
                StatusBar,
            ],
        };
    }

    // A docked pane: a title row with its buttons, over the content.
    private static Surface Pane(string? title, IReadOnlyList<Element?> actions, Element? content, SurfaceName surface) => new()
    {
        SurfaceColor = surface,
        Layout = new LayoutStyle { FlexGrow = 1 },
        Children =
        [
            title is null ? null : new Box
            {
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, Height = 36, Padding = new Edges(16, 0, 4, 0), FlexShrink = 0 },
                Children =
                [
                    new SurfaceText(title) { TextType = TextType.LabelLarge, Legibility = Legibility.Medium, MaxLines = 1, Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 } },
                    .. actions,
                ],
            },
            new Box { Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 }, Children = [content] },
        ],
    };

    /// <summary>The narrow bar of activity icons, the chosen one marked by a bar on its left.</summary>
    private sealed record ActivityBar(IReadOnlyList<NavItem> Items, int Selected, Action<int>? OnSelect) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var select = OnSelect;
            var buttons = new List<Element?>();
            for (var i = 0; i < Items.Count; i++)
            {
                var index = i;
                var item = Items[i];
                var chosen = i == Selected;
                buttons.Add(new Box
                {
                    Layout = new LayoutStyle { AlignItems = Align.Center },
                    Children =
                    [
                        new PressableSurface
                        {
                            Role = SemanticsRole.Tab,
                            Label = item.Label,
                            Selected = chosen,
                            ContentLegibility = chosen ? null : Legibility.Medium,
                            CornerShape = CornerShapeRole.Small,
                            OnPress = () => select?.Invoke(index),
                            Layout = new LayoutStyle { Width = 40, Height = 40, AlignItems = Align.Center, JustifyContent = Justify.Center },
                            Children = [new SurfaceIcon(item.Icon) { IconSize = 22, IconFilled = chosen }],
                        },
                        chosen ? new Box
                        {
                            HitTestVisible = false,
                            Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(0, 8, Dimension.Undefined, 8), Width = 3 },
                            Background = theme.Get(SurfaceName.Primary),
                            CornerRadii = Radiant.Graphics2D.CornerRadii.All(1.5f),
                        } : null,
                    ],
                });
            }
            return new Surface
            {
                SurfaceColor = SurfaceName.SurfaceContainer,
                Semantics = new Semantics { Role = SemanticsRole.TabList, Label = "Activities" },
                Layout = new LayoutStyle { Width = 48, AlignItems = Align.Stretch, Padding = Edges.Symmetric(0, 6), RowGap = 4, FlexShrink = 0 },
                Children = buttons,
            };
        }
    }
}
