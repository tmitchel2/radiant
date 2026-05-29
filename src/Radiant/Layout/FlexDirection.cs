namespace Radiant.Layout;

/// <summary>Main-axis direction of a flex container (mirrors CSS <c>flex-direction</c>).</summary>
public enum FlexDirection
{
    /// <summary>Children stacked top-to-bottom (Yoga / Yoga-default).</summary>
    Column,

    /// <summary>Children stacked bottom-to-top.</summary>
    ColumnReverse,

    /// <summary>Children laid out left-to-right.</summary>
    Row,

    /// <summary>Children laid out right-to-left.</summary>
    RowReverse,
}
