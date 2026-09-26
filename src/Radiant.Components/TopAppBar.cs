using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// The bar across the top of a screen: an optional navigation button, the title, and actions on
/// the right. It takes the container colour once content scrolls under it.
/// </summary>
/// <param name="Title">The screen's title.</param>
public sealed record TopAppBar(string Title) : Component
{
    /// <summary>The navigation button's icon (a menu or back arrow); null for none.</summary>
    public string? NavigationIcon { get; init; }

    /// <summary>What the navigation button does.</summary>
    public Action? OnNavigation { get; init; }

    /// <summary>What assistive technology calls the navigation button ("Back", "Open menu").</summary>
    public string NavigationLabel { get; init; } = "Navigate";

    /// <summary>Action buttons on the right (usually icon buttons).</summary>
    public IReadOnlyList<Element?> Actions { get; init; } = [];

    /// <summary>Whether content is scrolled under it (it takes the container colour).</summary>
    public bool Scrolled { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new Surface
        {
            SurfaceColor = Scrolled ? SurfaceName.SurfaceContainer : SurfaceName.Surface,
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = Title },
            Layout = new LayoutStyle
            {
                FlexDirection = FlexDirection.Row,
                AlignItems = Align.Center,
                Height = 64,
                Padding = Edges.Symmetric(4, 0),
                ColumnGap = 4,
                AlignSelf = Align.Stretch,
            },
            Children =
            [
                NavigationIcon is null ? new Box { Layout = new LayoutStyle { Width = 12 } } : new IconButton(NavigationIcon, NavigationLabel) { OnPress = OnNavigation },
                new SurfaceText(Title) { TextType = TextType.TitleLarge, MaxLines = 1, Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 } },
                .. Actions,
            ],
        };
    }
}
