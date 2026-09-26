using System;
using Radiant.Text;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>A column of a <see cref="DataTable"/>.</summary>
/// <param name="Header">The column's name, in the header.</param>
/// <param name="Cell">Builds the column's cell for a row (by its index in display order).</param>
public sealed record DataColumn(string Header, Func<int, Element?> Cell)
{
    /// <summary>The column's width at first; the user can drag it wider or narrower.</summary>
    public float Width { get; init; } = 160f;

    /// <summary>The least the user can drag it to.</summary>
    public float MinWidth { get; init; } = 60f;

    /// <summary>Whether it takes the table's spare width (it can't then be dragged).</summary>
    public bool Grow { get; init; }

    /// <summary>Whether pressing its header sorts by it.</summary>
    public bool Sortable { get; init; }

    /// <summary>How its header and cells line up: numbers usually at the end.</summary>
    public TextAlignment Alignment { get; init; }
}
