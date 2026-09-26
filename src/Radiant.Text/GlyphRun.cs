using System.Numerics;

namespace Radiant.Text;

/// <summary>
/// Shaped glyphs placed in a paragraph: one font, one style, one direction, on one line. Draw the
/// glyphs from <see cref="Origin"/>, advancing by <see cref="ShapedRun.Advances"/>.
/// </summary>
public sealed class GlyphRun
{
    internal GlyphRun(ShapedRun shaped, TextStyle style, Vector2 origin, int start, int end, bool isEllipsis)
    {
        Shaped = shaped;
        Style = style;
        Origin = origin;
        Start = start;
        End = end;
        IsEllipsis = isEllipsis;
    }

    /// <summary>The glyphs, their advances and offsets.</summary>
    public ShapedRun Shaped { get; }

    /// <summary>The style the text was set in (its color, for drawing).</summary>
    public TextStyle Style { get; }

    /// <summary>Where the first glyph's pen starts: its left edge on the baseline, in paragraph pixels.</summary>
    public Vector2 Origin { get; }

    /// <summary>The first UTF-16 index of the text the glyphs come from.</summary>
    public int Start { get; }

    /// <summary>One past the last UTF-16 index; equal to <see cref="Start"/> for an ellipsis.</summary>
    public int End { get; }

    /// <summary>Whether this is the ellipsis marking cut text rather than text from the paragraph.</summary>
    public bool IsEllipsis { get; }

    /// <summary>The direction the run reads in.</summary>
    public TextDirection Direction => Shaped.Direction;

    /// <summary>The run's width, in pixels.</summary>
    public float Width => Shaped.Width;
}
