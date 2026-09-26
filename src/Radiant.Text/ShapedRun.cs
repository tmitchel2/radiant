using System;
using System.Collections.Generic;
using System.Numerics;

namespace Radiant.Text;

/// <summary>
/// A run of text turned into positioned glyphs by <see cref="TextShaper"/>, in pixels with y down.
/// Glyphs are in visual order (left to right on screen): for right-to-left text, the first glyph
/// is the last character's.
/// </summary>
public sealed class ShapedRun
{
    internal ShapedRun(FontInstance font, float size, TextDirection direction, int start, int length,
        uint[] glyphs, int[] clusters, float[] advances, Vector2[] offsets)
    {
        Font = font;
        Size = size;
        Direction = direction;
        Start = start;
        Length = length;
        Glyphs = glyphs;
        Clusters = clusters;
        Advances = advances;
        Offsets = offsets;
        var width = 0f;
        foreach (var advance in advances)
        {
            width += advance;
        }
        Width = width;
    }

    /// <summary>The font the glyphs belong to.</summary>
    public FontInstance Font { get; }

    /// <summary>The size shaped at: the em in pixels.</summary>
    public float Size { get; }

    /// <summary>The direction the run reads in.</summary>
    public TextDirection Direction { get; }

    /// <summary>The first UTF-16 index of the run in the text it was shaped from.</summary>
    public int Start { get; }

    /// <summary>The run's length in UTF-16 code units.</summary>
    public int Length { get; }

    /// <summary>The glyph ids, in visual order.</summary>
    public IReadOnlyList<uint> Glyphs { get; }

    /// <summary>
    /// For each glyph, the UTF-16 index in the text of the first character it came from. Several
    /// glyphs can share a cluster (a decomposed accent) and one glyph can stand for several
    /// characters (a ligature), so this maps glyphs back to text for carets and hit testing.
    /// </summary>
    public IReadOnlyList<int> Clusters { get; }

    /// <summary>How far each glyph moves the pen, in pixels.</summary>
    public IReadOnlyList<float> Advances { get; }

    /// <summary>Each glyph's offset from the pen position, in pixels (x right, y down).</summary>
    public IReadOnlyList<Vector2> Offsets { get; }

    /// <summary>The run's total advance, in pixels.</summary>
    public float Width { get; }

    /// <summary>The number of glyphs.</summary>
    public int Count => Glyphs.Count;

    /// <summary>Each glyph's pen position, from the run's start, in pixels.</summary>
    public IEnumerable<float> PenPositions()
    {
        var x = 0f;
        foreach (var advance in Advances)
        {
            yield return x;
            x += advance;
        }
    }

    /// <summary>For debugging: the run's font, size, span and glyph count.</summary>
    public override string ToString() =>
        FormattableString.Invariant($"{Font} {Size}px [{Start}, {Start + Length}) {Count} glyphs {Width:0.##}px {Direction}");
}
