namespace Radiant.Layout;

/// <summary>
/// Flexbox + box-model layout inputs for a <see cref="Radiant.UI.UIElement"/>, mapped onto a Yoga
/// node by <see cref="YogaLayoutEngine"/>. Every property is "unset" by default (nullable enums and
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
}
