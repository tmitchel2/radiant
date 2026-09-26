using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A message in the page (not a pop-up): an icon and colour for its kind, a title, a line of text,
/// actions, and a close button if it can be dismissed. Assistive technology announces it as an
/// alert.
/// </summary>
/// <param name="Title">What it says, in brief.</param>
public sealed record Alert(string Title) : Component
{
    /// <summary>Info, success, warning or error.</summary>
    public AlertKind Kind { get; init; }

    /// <summary>More detail under the title.</summary>
    public string? Text { get; init; }

    /// <summary>Buttons after the text (usually text buttons).</summary>
    public IReadOnlyList<Element?> Actions { get; init; } = [];

    /// <summary>What the close button does; null for no close button.</summary>
    public Action? OnDismiss { get; init; }

    /// <summary>The alert's size and placement.</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        var (surface, icon) = Kind switch
        {
            AlertKind.Success => (SurfaceName.Success, "check_circle"),
            AlertKind.Warning => (SurfaceName.Warning, "warning"),
            AlertKind.Error => (SurfaceName.Error, "error"),
            _ => (SurfaceName.Info, "info"),
        };
        return new Surface
        {
            SurfaceColor = surface,
            SurfaceContainerToggle = true,
            CornerShape = CornerShapeRole.Medium,
            Semantics = new Semantics { Role = SemanticsRole.Alert, Label = Title, Description = Text },
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.FlexStart, ColumnGap = 12, Padding = new Edges(16, 12, OnDismiss is null ? 16 : 4, 12), AlignSelf = Align.Stretch }.Merge(Layout ?? default),
            Children =
            [
                new SurfaceIcon(icon) { IconSize = 20, Layout = new LayoutStyle { Margin = new Edges(0, 2, 0, 0) } },
                new Box
                {
                    Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, RowGap = 4 },
                    Children =
                    [
                        new SurfaceText(Title) { TextType = TextType.TitleSmall },
                        Text is null ? null : new SurfaceText(Text) { Legibility = Legibility.High },
                        Actions.Count == 0 ? null : new Box
                        {
                            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = 8, Margin = new Edges(-12, 4, 0, 0) },
                            Children = Actions,
                        },
                    ],
                },
                OnDismiss is null ? null : new IconButton("close", "Dismiss") { OnPress = OnDismiss, Layout = new LayoutStyle { Width = 32, Height = 32 } },
            ],
        };
    }
}
