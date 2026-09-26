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
    private readonly uint[] _glyphs;
    private readonly int[] _clusters;
    private readonly float[] _advances;
    private readonly Vector2[] _offsets;
    private readonly bool[] _unsafeToBreak;

    internal ShapedRun(FontInstance font, float size, TextDirection direction, int start, int length,
        uint[] glyphs, int[] clusters, float[] advances, Vector2[] offsets, bool[] unsafeToBreak)
    {
        _glyphs = glyphs;
        _clusters = clusters;
        _advances = advances;
        _offsets = offsets;
        _unsafeToBreak = unsafeToBreak;
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

    /// <summary>
    /// The glyphs for <c>text[start..end)</c>, a part of this run, taken without shaping again:
    /// null where HarfBuzz says cutting there could change the glyphs (say, a ligature or joining
    /// letters across the cut), so the part must be reshaped.
    /// </summary>
    internal ShapedRun? Slice(int start, int end)
    {
        if (!IsSafeCut(start) || !IsSafeCut(end))
        {
            return null;
        }
        // Clusters are monotonic (HarfBuzz's default cluster level), rising in left-to-right runs
        // and falling in right-to-left ones, so a range of text is a contiguous range of glyphs.
        var from = Direction == TextDirection.LeftToRight ? FirstGlyphAtOrAfter(start) : FirstGlyphBefore(end);
        var to = Direction == TextDirection.LeftToRight ? FirstGlyphAtOrAfter(end) : FirstGlyphBefore(start);
        return new ShapedRun(Font, Size, Direction, start, end - start,
            _glyphs[from..to], _clusters[from..to], _advances[from..to], _offsets[from..to], _unsafeToBreak[from..to]);
    }

    // A cut is safe at the run's ends, or at the start of a cluster none of whose glyphs HarfBuzz
    // flagged unsafe to break before.
    private bool IsSafeCut(int index)
    {
        if (index <= Start || index >= Start + Length)
        {
            return true;
        }
        var first = Direction == TextDirection.LeftToRight ? FirstGlyphAtOrAfter(index) : FirstGlyphBefore(index + 1);
        if (first >= _clusters.Length || _clusters[first] != index)
        {
            return false; // the cut is inside a cluster
        }
        for (var i = first; i < _clusters.Length && _clusters[i] == index; i++)
        {
            if (_unsafeToBreak[i])
            {
                return false;
            }
        }
        return true;
    }

    // In a left-to-right run: the first glyph whose cluster is at or after the index.
    private int FirstGlyphAtOrAfter(int index)
    {
        int lo = 0, hi = _clusters.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (_clusters[mid] < index)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        return lo;
    }

    // In a right-to-left run: the first glyph whose cluster is before the index.
    private int FirstGlyphBefore(int index)
    {
        int lo = 0, hi = _clusters.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (_clusters[mid] >= index)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        return lo;
    }

    /// <summary>For debugging: the run's font, size, span and glyph count.</summary>
    public override string ToString() =>
        FormattableString.Invariant($"{Font} {Size}px [{Start}, {Start + Length}) {Count} glyphs {Width:0.##}px {Direction}");
}
