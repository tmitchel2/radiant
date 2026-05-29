namespace Radiant.Layout;

/// <summary>Main-axis distribution of children (mirrors CSS <c>justify-content</c>).</summary>
public enum Justify
{
    /// <summary>Pack children toward the start of the main axis.</summary>
    FlexStart,

    /// <summary>Center children along the main axis.</summary>
    Center,

    /// <summary>Pack children toward the end of the main axis.</summary>
    FlexEnd,

    /// <summary>Distribute children with the first/last flush to the edges.</summary>
    SpaceBetween,

    /// <summary>Distribute children with equal space around each.</summary>
    SpaceAround,

    /// <summary>Distribute children with equal space between and at the edges.</summary>
    SpaceEvenly,
}
