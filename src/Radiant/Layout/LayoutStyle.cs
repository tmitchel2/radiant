namespace Radiant.Layout;

/// <summary>
/// Flexbox + box-model layout inputs for an element, mapped onto its render node's Yoga node (in
/// <c>Radiant.UI.Core</c>). Every property is "unset" by default (nullable enums and
/// <see cref="Dimension.Undefined"/> lengths), so <c>default(LayoutStyle)</c> applies no overrides
/// and the node keeps Yoga's defaults. Build with object/<c>with</c> initialisers:
/// <code>new LayoutStyle { FlexDirection = FlexDirection.Row, FlexGrow = 1f, Padding = Edges.All(8f) }</code>
/// </summary>
public readonly record struct LayoutStyle
{
    /// <summary>Main-axis direction of this container's children.</summary>
    public FlexDirection? FlexDirection { get; init; }

    /// <summary>Main-axis distribution of this container's children.</summary>
    public Justify? JustifyContent { get; init; }

    /// <summary>Default cross-axis alignment for this container's children.</summary>
    public Align? AlignItems { get; init; }

    /// <summary>Cross-axis alignment override for this item within its parent.</summary>
    public Align? AlignSelf { get; init; }

    /// <summary>Whether this container's children wrap onto multiple lines.</summary>
    public FlexWrap? FlexWrap { get; init; }

    /// <summary>Positioning scheme for this item.</summary>
    public PositionType? Position { get; init; }

    /// <summary>Growth factor for distributing free main-axis space.</summary>
    public float? FlexGrow { get; init; }

    /// <summary>Shrink factor for absorbing main-axis overflow.</summary>
    public float? FlexShrink { get; init; }

    /// <summary>Initial main-axis size before grow/shrink.</summary>
    public Dimension FlexBasis { get; init; }

    /// <summary>Fixed width.</summary>
    public Dimension Width { get; init; }

    /// <summary>Fixed height.</summary>
    public Dimension Height { get; init; }

    /// <summary>Minimum width.</summary>
    public Dimension MinWidth { get; init; }

    /// <summary>Minimum height.</summary>
    public Dimension MinHeight { get; init; }

    /// <summary>Maximum width.</summary>
    public Dimension MaxWidth { get; init; }

    /// <summary>Maximum height.</summary>
    public Dimension MaxHeight { get; init; }

    /// <summary>Outer margin.</summary>
    public Edges Margin { get; init; }

    /// <summary>Inner padding (the content box inset; consumed by children's layout).</summary>
    public Edges Padding { get; init; }

    /// <summary>Position insets (left/top/right/bottom) for relative/absolute items.</summary>
    public Edges Inset { get; init; }

    /// <summary>Gap between rows (cross-axis spacing for wrapped/column content).</summary>
    public Dimension RowGap { get; init; }

    /// <summary>Gap between columns (main-axis spacing for row content).</summary>
    public Dimension ColumnGap { get; init; }

    /// <summary>Width-to-height aspect ratio constraint.</summary>
    public float? AspectRatio { get; init; }

    /// <summary>
    /// Which way this node and its descendants lay out: rows run from the start side and edges'
    /// starts are on it (left for left-to-right). Inherited when null.
    /// </summary>
    public Radiant.Text.TextDirection? Direction { get; init; }

    /// <summary>
    /// This style with everything <paramref name="over"/> sets replacing this one's, edge by edge
    /// for margin, padding and inset: how a component's own layout takes a caller's additions
    /// (a button told to stretch keeps its padding).
    /// </summary>
    public LayoutStyle Merge(LayoutStyle over) => new()
    {
        FlexDirection = over.FlexDirection ?? FlexDirection,
        JustifyContent = over.JustifyContent ?? JustifyContent,
        AlignItems = over.AlignItems ?? AlignItems,
        AlignSelf = over.AlignSelf ?? AlignSelf,
        FlexWrap = over.FlexWrap ?? FlexWrap,
        Position = over.Position ?? Position,
        FlexGrow = over.FlexGrow ?? FlexGrow,
        FlexShrink = over.FlexShrink ?? FlexShrink,
        FlexBasis = over.FlexBasis.IsSet ? over.FlexBasis : FlexBasis,
        Width = over.Width.IsSet ? over.Width : Width,
        Height = over.Height.IsSet ? over.Height : Height,
        MinWidth = over.MinWidth.IsSet ? over.MinWidth : MinWidth,
        MinHeight = over.MinHeight.IsSet ? over.MinHeight : MinHeight,
        MaxWidth = over.MaxWidth.IsSet ? over.MaxWidth : MaxWidth,
        MaxHeight = over.MaxHeight.IsSet ? over.MaxHeight : MaxHeight,
        Margin = Margin.Merge(over.Margin),
        Padding = Padding.Merge(over.Padding),
        Inset = Inset.Merge(over.Inset),
        RowGap = over.RowGap.IsSet ? over.RowGap : RowGap,
        ColumnGap = over.ColumnGap.IsSet ? over.ColumnGap : ColumnGap,
        AspectRatio = over.AspectRatio ?? AspectRatio,
        Direction = over.Direction ?? Direction,
    };
}
