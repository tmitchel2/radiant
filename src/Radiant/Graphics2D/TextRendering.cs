namespace Radiant.Graphics2D;

/// <summary>How laid-out text (<see cref="Renderer2D.DrawParagraph"/>) is drawn.</summary>
public enum TextRendering
{
    /// <summary>
    /// Glyphs rasterized on the CPU at their on-screen size into a coverage atlas, snapped to the
    /// pixel grid: the sharpest small text, softer when rotated, and a new bitmap per size.
    /// </summary>
    Coverage,

    /// <summary>
    /// Multi-channel signed distance fields generated per glyph once and drawn at any size or
    /// angle: sharp corners, smooth scaling, a little softer than coverage at small sizes.
    /// </summary>
    Msdf,

    /// <summary>Coverage up to <see cref="Renderer2D.HybridThreshold"/> device pixels and when not rotated; MSDF otherwise.</summary>
    Hybrid,

    /// <summary>
    /// Glyph outlines evaluated per pixel on the GPU (Eric Lengyel's Slug algorithm): exact at any
    /// size and transform, with no atlas, at a higher cost per pixel.
    /// </summary>
    Slug,
}
