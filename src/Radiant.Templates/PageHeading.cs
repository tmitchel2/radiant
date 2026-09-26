using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>A page's heading: its title, a line of description, and actions on the right.</summary>
/// <param name="Title">The title.</param>
public sealed record PageHeading(string Title) : Component
{
    /// <summary>A line under the title.</summary>
    public string? Description { get; init; }

    /// <summary>Buttons on the right.</summary>
    public IReadOnlyList<Element?> Actions { get; init; } = [];

    /// <inheritdoc/>
    public override Element? Build(BuildContext context) => new Box
    {
        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 16, FlexWrap = FlexWrap.Wrap, RowGap = 12 },
        Children =
        [
            new Box
            {
                Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1, MinWidth = 200, RowGap = 4 },
                Children =
                [
                    new SurfaceText(Title) { TextType = TextType.HeadlineSmall, HeadingLevel = 1 },
                    Description is null ? null : new SurfaceText(Description) { Legibility = Legibility.Medium },
                ],
            },
            new Box { Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = 8 }, Children = Actions },
        ],
    };
}
