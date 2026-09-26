using System.Collections.Generic;

namespace Radiant.Components;

/// <summary>The panels in one area of a <see cref="DockLayout"/>, as tabs.</summary>
/// <param name="Items">The panels' ids, in tab order.</param>
/// <param name="Active">The one shown; the first if null.</param>
/// <param name="Size">The area's width (or the bottom's height); the centre takes the rest.</param>
public sealed record DockGroup(IReadOnlyList<string> Items, string? Active = null, float Size = 260f)
{
    /// <summary>The panel shown: <see cref="Active"/> if it's here, otherwise the first.</summary>
    public string? Shown => Active is { } id && Contains(id) ? id : Items.Count > 0 ? Items[0] : null;

    /// <summary>Whether the panel is here.</summary>
    public bool Contains(string id)
    {
        foreach (var item in Items)
        {
            if (item == id)
            {
                return true;
            }
        }
        return false;
    }
}
