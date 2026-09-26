using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// A collapsible group of an inspector's properties: a header that folds it away, over
/// <see cref="PropertyRow"/>s. It keeps whether it's open itself, starting from <see cref="Open"/>.
/// </summary>
/// <param name="Title">The group's name.</param>
/// <param name="Rows">Its properties, usually <see cref="PropertyRow"/>s.</param>
public sealed record InspectorSection(string Title, IReadOnlyList<Element?> Rows) : Component
{
    /// <summary>Whether it starts open.</summary>
    public bool Open { get; init; } = true;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        var open = context.UseState(Open);
        return new Box
        {
            Children =
            [
                new PressableSurface
                {
                    InsetFocusRing = true,
                    Role = SemanticsRole.Button,
                    Label = Title,
                    Expanded = open.Value,
                    OnPress = () => open.Set(!open.Value),
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 4, Height = 32, Padding = Edges.Symmetric(8, 0) },
                    Children =
                    [
                        new SurfaceIcon(open.Value ? "expand_more" : "chevron_right") { IconSize = 18, Legibility = Legibility.Medium },
                        new SurfaceText(Title) { TextType = TextType.TitleSmall },
                    ],
                },
                open.Value ? new Box { Layout = new LayoutStyle { Padding = new Edges(16, 0, 12, 8), RowGap = 6 }, Children = Rows } : null,
                new Divider(),
            ],
        };
    }
}
