using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Facts as terms beside their details (a record's fields, an order's summary), divided by lines;
/// when the width is too narrow for both side by side, each detail goes under its term.
/// </summary>
/// <param name="Items">The terms and their details (text, or any element).</param>
public sealed record DescriptionList(IReadOnlyList<(string Term, Element? Detail)> Items) : Component
{
    /// <summary>The terms' column width, side by side.</summary>
    public float TermWidth { get; init; } = 200f;

    /// <summary>A text detail, as the list shows it.</summary>
    public static Element Text(string text) => new SurfaceText(text);

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        var rows = new List<Element?>();
        for (var i = 0; i < Items.Count; i++)
        {
            if (i > 0)
            {
                rows.Add(new Divider());
            }
            var (term, detail) = Items[i];
            rows.Add(new Box
            {
                Semantics = new Semantics { Role = SemanticsRole.Group, Label = term },
                // Wraps the detail under the term when there isn't room beside it.
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, FlexWrap = FlexWrap.Wrap, ColumnGap = 24, RowGap = 4, Padding = Edges.Symmetric(0, 14) },
                Children =
                [
                    new SurfaceText(term) { TextType = TextType.LabelLarge, Legibility = Legibility.Medium, Layout = new LayoutStyle { Width = TermWidth, FlexShrink = 0 } },
                    new Box { Layout = new LayoutStyle { FlexGrow = 1, FlexBasis = 200, AlignItems = Align.FlexStart }, Children = [detail] },
                ],
            });
        }
        return new Box { Layout = new LayoutStyle { AlignSelf = Align.Stretch }, Children = rows };
    }
}
