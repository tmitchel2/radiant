using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Toggle buttons that go together: exactly one on (text alignment, a view mode) or any number
/// (bold, italic, underline) with <see cref="Multiple"/>. Controlled: shows
/// <paramref name="Selected"/> and reports each change.
/// </summary>
/// <param name="Items">The buttons' icons and labels.</param>
/// <param name="Selected">Which are on.</param>
/// <param name="OnChange">Called with which should be on.</param>
public sealed record ToggleGroup(IReadOnlyList<(string Icon, string Label)> Items, IReadOnlySet<int> Selected, Action<IReadOnlySet<int>>? OnChange) : Component
{
    /// <summary>Whether several can be on, and all off.</summary>
    public bool Multiple { get; init; }

    /// <summary>What assistive technology calls the group.</summary>
    public string? Label { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        var (selected, multiple, change) = (Selected, Multiple, OnChange);
        var buttons = new List<Element?>();
        for (var i = 0; i < Items.Count; i++)
        {
            var index = i;
            buttons.Add(new ToggleButton(Items[i].Icon, Items[i].Label, selected.Contains(i), on =>
            {
                // One-of groups can't be switched off: pressing the one that's on keeps it on.
                var next = multiple ? new HashSet<int>(selected) : [];
                if (on || !multiple)
                {
                    next.Add(index);
                }
                else
                {
                    next.Remove(index);
                }
                if (!next.SetEquals(selected))
                {
                    change?.Invoke(next);
                }
            }));
        }
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = Label },
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = 2 },
            Children = buttons,
        };
    }
}
