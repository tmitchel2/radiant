using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A row of controls for the thing below it (a formatting bar, an editor's actions): Left and Right
/// move focus between its controls, stopping at its ends, and Home and End go to them. Put
/// <see cref="Divider"/>s (vertical) between groups.
/// </summary>
/// <param name="Items">The controls.</param>
public sealed record Toolbar(IReadOnlyList<Element?> Items) : Component
{
    /// <summary>What assistive technology calls it.</summary>
    public string? Label { get; init; }

    /// <summary>The bar's size and placement.</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var bar = context.UseRef(new ElementRef()).Value;
        var root = context.Root;

        // Moves focus once along the tab order, stepping back if that left the bar.
        bool Step(bool forward)
        {
            root.MoveFocus(forward);
            if (bar.ContainsFocus)
            {
                return true;
            }
            root.MoveFocus(!forward);
            return false;
        }

        return new Box
        {
            Ref = bar,
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = Label ?? "Toolbar" },
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 2, Height = 44, Padding = Edges.Symmetric(4, 0) }.Merge(Layout ?? default),
            OnKeyDown = e =>
            {
                switch (e.Key)
                {
                    case KeyCode.Left or KeyCode.Right:
                        Step(e.Key == KeyCode.Right);
                        e.Handled = true;
                        break;
                    case KeyCode.Home or KeyCode.End:
                        while (Step(e.Key == KeyCode.End))
                        {
                        }
                        e.Handled = true;
                        break;
                    default:
                        break;
                }
            },
            Children = Items,
        };
    }
}
