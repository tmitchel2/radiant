using System.Collections.Generic;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>
/// A group of settings: a title and description on the left, the controls in a card on the right
/// (stacked when narrow), as settings pages lay out.
/// </summary>
/// <param name="Title">The group's name.</param>
/// <param name="Rows">Its settings, usually <see cref="SettingsRow"/>s.</param>
public sealed record SettingsSection(string Title, IReadOnlyList<Element?> Rows) : Component
{
    /// <summary>What the group covers.</summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        var rows = new List<Element?>();
        for (var i = 0; i < Rows.Count; i++)
        {
            if (i > 0)
            {
                rows.Add(new Divider());
            }
            rows.Add(Rows[i]);
        }
        return new Box
        {
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexWrap = FlexWrap.Wrap, ColumnGap = 32, RowGap = 12 },
            Children =
            [
                new Box
                {
                    Layout = new LayoutStyle { FlexBasis = 240, FlexGrow = 1, RowGap = 4 },
                    Children =
                    [
                        new SurfaceText(Title) { TextType = TextType.TitleMedium, HeadingLevel = 2 },
                        Description is null ? null : new SurfaceText(Description) { Legibility = Legibility.Medium },
                    ],
                },
                new Card(rows.ToArray())
                {
                    Variant = CardVariant.Outlined,
                    Layout = new LayoutStyle { FlexBasis = 420, FlexGrow = 2, Padding = Edges.Symmetric(0, 4) },
                },
            ],
        };
    }
}
