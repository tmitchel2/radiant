using System;
using System.Collections.Generic;
using System.Linq;

namespace Radiant.Components;

/// <summary>
/// Which panels a <see cref="DockPanel"/> shows where, and how big each area is: immutable, so an
/// app can keep it (and save it between runs) and each change is a new layout.
/// </summary>
public sealed record DockLayout
{
    /// <summary>The panels on the left.</summary>
    public DockGroup Left { get; init; } = new([]);

    /// <summary>The panels on the right.</summary>
    public DockGroup Right { get; init; } = new([]);

    /// <summary>The panels along the bottom.</summary>
    public DockGroup Bottom { get; init; } = new([], Size: 200f);

    /// <summary>The panels in the middle.</summary>
    public DockGroup Center { get; init; } = new([]);

    /// <summary>The group in an area.</summary>
    public DockGroup this[DockArea area] => area switch
    {
        DockArea.Left => Left,
        DockArea.Right => Right,
        DockArea.Bottom => Bottom,
        _ => Center,
    };

    /// <summary>This layout with <paramref name="area"/>'s group replaced.</summary>
    public DockLayout With(DockArea area, DockGroup group) => area switch
    {
        DockArea.Left => this with { Left = group },
        DockArea.Right => this with { Right = group },
        DockArea.Bottom => this with { Bottom = group },
        _ => this with { Center = group },
    };

    /// <summary>The area the panel is in, or null if it isn't shown.</summary>
    public DockArea? AreaOf(string id) =>
        Enum.GetValues<DockArea>().Cast<DockArea?>().FirstOrDefault(a => this[a!.Value].Contains(id));

    /// <summary>The panel moved to the end of <paramref name="to"/>'s tabs, and shown there; a panel not in the layout is added.</summary>
    public DockLayout Move(string id, DockArea to)
    {
        var without = Close(id);
        var group = without[to];
        return without.With(to, group with { Items = [.. group.Items, id], Active = id });
    }

    /// <summary>The panel taken out of the layout; its neighbour is shown in its place if it was.</summary>
    public DockLayout Close(string id)
    {
        if (AreaOf(id) is not { } area)
        {
            return this;
        }
        var group = this[area];
        var at = group.Items.ToList().IndexOf(id);
        List<string> items = [.. group.Items.Where(i => i != id)];
        var active = group.Shown == id ? (items.Count == 0 ? null : items[Math.Min(at, items.Count - 1)]) : group.Active;
        return With(area, group with { Items = items, Active = active });
    }

    /// <summary>The panel shown in its area.</summary>
    public DockLayout Activate(string id) => AreaOf(id) is { } area ? With(area, this[area] with { Active = id }) : this;

    /// <summary>The area's size set.</summary>
    public DockLayout Resize(DockArea area, float size) => With(area, this[area] with { Size = size });
}
