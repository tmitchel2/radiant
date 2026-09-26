using System.Collections.Generic;

namespace Radiant.Text;

/// <summary>One line of a laid-out <see cref="Paragraph"/>.</summary>
public sealed class TextLine
{
    internal TextLine(int index, int start, int end, int lineBreakLength, TextDirection direction,
        float top, float ascent, float descent, float left, float width, float contentWidth,
        bool isEllipsized, GlyphRun[] runs, ClusterBox[] boxes)
    {
        Index = index;
        Start = start;
        End = end;
        LineBreakLength = lineBreakLength;
        Direction = direction;
        Top = top;
        Ascent = ascent;
        Descent = descent;
        Left = left;
        Width = width;
        ContentWidth = contentWidth;
        IsEllipsized = isEllipsized;
        Runs = runs;
        Boxes = boxes;
    }

    /// <summary>The line's number, from 0.</summary>
    public int Index { get; }

    /// <summary>The first UTF-16 index on the line.</summary>
    public int Start { get; }

    /// <summary>One past the last UTF-16 index on the line, including trailing spaces and any line break.</summary>
    public int End { get; }

    /// <summary>
    /// How many code units at the end of the line are a line break (a newline, CR LF, or a
    /// paragraph or line separator): 0 when the line wrapped or ends the text.
    /// </summary>
    public int LineBreakLength { get; }

    /// <summary>The direction of the paragraph (between line breaks) the line belongs to.</summary>
    public TextDirection Direction { get; }

    /// <summary>The top of the line box, in paragraph pixels.</summary>
    public float Top { get; }

    /// <summary>How far the line box reaches above the baseline (with half the leading).</summary>
    public float Ascent { get; }

    /// <summary>How far the line box reaches below the baseline (with half the leading).</summary>
    public float Descent { get; }

    /// <summary>The baseline, in paragraph pixels.</summary>
    public float Baseline => Top + Ascent;

    /// <summary>The line box's height.</summary>
    public float Height => Ascent + Descent;

    /// <summary>The bottom of the line box.</summary>
    public float Bottom => Top + Height;

    /// <summary>The left edge of the line's glyphs, trailing spaces included, after alignment.</summary>
    public float Left { get; }

    /// <summary>The width of all the line's glyphs, trailing spaces included.</summary>
    public float Width { get; }

    /// <summary>The width without trailing spaces, which hang outside the line for alignment.</summary>
    public float ContentWidth { get; }

    /// <summary>Whether text was cut from the end of this line and an ellipsis put in its place.</summary>
    public bool IsEllipsized { get; }

    /// <summary>The line's glyph runs, left to right on screen.</summary>
    public IReadOnlyList<GlyphRun> Runs { get; }

    /// <summary>The line's graphemes, left to right on screen.</summary>
    internal ClusterBox[] Boxes { get; }

    /// <summary>The end of the line's text, before any line break: the last place a caret can go on it.</summary>
    internal int ContentEnd => End - LineBreakLength;
}
