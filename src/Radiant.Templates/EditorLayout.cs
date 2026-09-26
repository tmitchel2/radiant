using System;
using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// A document editor's window: a header across the top with the document's title, facts about it
/// (tags, a status) and its actions (undo, save, export), then a side panel (a chat, an outline)
/// beside the main area, set apart by lines. Leave out the panel (null) and the main area takes
/// the width.
/// </summary>
/// <param name="Title">The document's name, the window's heading.</param>
/// <param name="Content">The main area.</param>
public sealed record EditorLayout(string Title, Element? Content) : Component
{
    /// <summary>Facts after the title (usually <see cref="Tag"/>s).</summary>
    public IReadOnlyList<Element?> Meta { get; init; } = [];

    /// <summary>The header's actions, at its end.</summary>
    public IReadOnlyList<Element?> Actions { get; init; } = [];

    /// <summary>The side panel, or null for none.</summary>
    public Element? Panel { get; init; }

    /// <summary>The side panel's width.</summary>
    public float PanelWidth { get; init; } = 360f;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var style = context.UseTheme().Theme.Components.Navigation;
        return new Surface
        {
            SurfaceColor = SurfaceName.Surface,
            Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
            Children =
            [
                new Box
                {
                    Semantics = new Semantics { Role = SemanticsRole.Group, Label = Title },
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, Height = style.AppBarHeight, Padding = Edges.Symmetric(16, 0), ColumnGap = 8 },
                    Children =
                    [
                        new SurfaceText(Title) { TextType = style.AppBarTitle, HeadingLevel = 1, MaxLines = 1, Layout = new LayoutStyle { FlexShrink = 1, Margin = new Edges(0, 0, 8, 0) } },
                        .. Meta,
                        new Box { Layout = new LayoutStyle { FlexGrow = 1 } },
                        .. Actions,
                    ],
                },
                new Divider(),
                new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexGrow = 1, FlexShrink = 1 },
                    Children =
                    [
                        Panel is null ? null : new Box { Layout = new LayoutStyle { Width = PanelWidth, FlexShrink = 0, Padding = Edges.All(16), RowGap = 12 }, Children = [Panel] },
                        Panel is null ? null : new Divider { Vertical = true },
                        new Box { Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, Padding = Edges.All(16), RowGap = 12 }, Children = [Content] },
                    ],
                },
            ],
        };
    }
}
