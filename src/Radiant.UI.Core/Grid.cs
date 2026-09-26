using System.Collections.Generic;
using Radiant.Layout;

namespace Radiant.UI.Core;

/// <summary>
/// Lays its children out in equal columns, row after row: a fixed number of columns, or as many
/// as fit at least <see cref="MinColumnWidth"/> wide. Unlike a wrapping flex row, every cell is
/// the same width, so a short last row lines up with the rows above; the cells in a row are as
/// tall as its tallest. Children's own widths and flex sizes are overridden by the column width.
/// </summary>
public sealed record Grid : HostElement
{
    /// <summary>The grid's own size and placement (its direction, wrapping and gaps are the grid's).</summary>
    public LayoutStyle Layout { get; init; }

    /// <summary>A fixed number of columns, or null to fit as many as <see cref="MinColumnWidth"/> allows.</summary>
    public int? Columns { get; init; }

    /// <summary>The narrowest a column may be when the count is not fixed.</summary>
    public float MinColumnWidth { get; init; } = 200f;

    /// <summary>The most columns when the count is not fixed.</summary>
    public int MaxColumns { get; init; } = int.MaxValue;

    /// <summary>The space between columns.</summary>
    public float ColumnGap { get; init; }

    /// <summary>The space between rows.</summary>
    public float RowGap { get; init; }

    /// <summary>The cells, in reading order.</summary>
    public IReadOnlyList<Element?> Children { get; init; } = [];

    /// <summary>How many columns a content width holds.</summary>
    internal int ColumnCount(float width)
    {
        if (Columns is { } fixedCount)
        {
            return System.Math.Max(1, fixedCount);
        }
        var fit = (int)System.MathF.Floor((width + ColumnGap) / (System.MathF.Max(MinColumnWidth, 1f) + ColumnGap));
        return System.Math.Clamp(fit, 1, System.Math.Max(1, MaxColumns));
    }

    internal override RenderNode CreateRenderNode() => new GridRenderNode();

    internal override IReadOnlyList<Element?> ChildElements => Children;
}
