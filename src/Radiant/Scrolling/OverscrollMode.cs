namespace Radiant.Scrolling;

/// <summary>How a scroller behaves when dragged or flung past a content edge.</summary>
public enum OverscrollMode
{
    /// <summary>Hard stop at the edge; velocity is killed.</summary>
    Clamp,

    /// <summary>iOS-style rubber-band: stretch past the edge with resistance, then spring back.</summary>
    Bounce,

    /// <summary>Clamp at the edge like <see cref="Clamp"/> but record an overscroll amount for an edge-glow render.</summary>
    Glow,
}
