using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Radiant.Text.Unicode;

/// <summary>
/// A paragraph with its embedding levels resolved by the Unicode Bidirectional Algorithm (UAX #9,
/// Unicode 16.0.0): which characters read left to right and which right to left, and in what order
/// a line of it is displayed. Resolve once per paragraph; paragraph layout then asks for each
/// line's levels (rule L1) and visual order (L2) after breaking lines, since both depend on where
/// the line ends. Levels are per UTF-16 code unit so they index straight into the string; both
/// halves of a surrogate pair always share one level and never swap.
/// </summary>
/// <remarks>
/// <para>
/// The text is meant to be one paragraph. A paragraph separator (Bidi_Class B: LF, CR, U+2029, …)
/// inside it still ends a paragraph as rule P1 says: it closes every embedding, override and
/// isolate, gets the paragraph level, and no rule looks across it. Every paragraph in the text
/// shares one base level, though — the one given, or found by P2/P3 in the first paragraph — so
/// that <see cref="Direction"/> and <see cref="BaseLevel"/> describe the whole text. Split text at
/// paragraph separators first to give each paragraph its own automatic direction. The Unicode
/// conformance tests only ever put a separator at the end of the text, where this agrees with them.
/// </para>
/// <para>
/// Characters removed by rule X9 (embedding and override controls, PDF, BN) have no level in the
/// algorithm; here they take the level of the character before them, so they never break a run.
/// </para>
/// </remarks>
public sealed class BidiParagraph
{
    private readonly string? _text;
    private readonly BidiClass[] _classes;
    private readonly byte[] _levels;

    private BidiParagraph(string? text, BidiClass[] classes, TextDirection? direction)
    {
        _text = text;
        _classes = classes;
        BaseLevel = direction switch
        {
            TextDirection.LeftToRight => 0,
            TextDirection.RightToLeft => 1,
            // P2 and P3: the first strong character outside isolates; left to right if none.
            _ => (byte)Math.Max(0, BidiResolver.FirstStrongLevel(classes, 0, classes.Length)),
        };
        Direction = BaseLevel == 1 ? TextDirection.RightToLeft : TextDirection.LeftToRight;
        _levels = BidiResolver.Resolve(text, classes, BaseLevel);
        Levels = new ReadOnlyCollection<byte>(_levels);
    }

    /// <summary>The paragraph's base direction.</summary>
    public TextDirection Direction { get; }

    /// <summary>The paragraph embedding level: 0 left to right, 1 right to left.</summary>
    public byte BaseLevel { get; }

    /// <summary>
    /// The resolved level of every UTF-16 code unit, before the per-line rule L1: odd levels read
    /// right to left.
    /// </summary>
    public IReadOnlyList<byte> Levels { get; }

    /// <summary>
    /// Resolves one paragraph. A <paramref name="direction"/> of null finds it from the text
    /// (rules P2 and P3): the direction of the first strong character not inside an isolate, or
    /// left to right if there is none.
    /// </summary>
    public static BidiParagraph Resolve(string text, TextDirection? direction = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var classes = new BidiClass[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                classes[i] = BidiClassData.Get(char.ConvertToUtf32(c, text[i + 1]));
                // The low half is invisible to the algorithm (BN is removed by X9) and takes the
                // level of the high half.
                classes[++i] = BidiClass.BN;
            }
            else
            {
                classes[i] = BidiClassData.Get(c);
            }
        }
        return new BidiParagraph(text, classes, direction);
    }

    /// <summary>
    /// Resolves a paragraph given as Bidi_Class values rather than text, as the BidiTest.txt
    /// conformance data is. No character is a bracket or mirrored.
    /// </summary>
    internal static BidiParagraph Resolve(BidiClass[] classes, TextDirection? direction = null) =>
        new(null, classes, direction);

    /// <summary>
    /// The levels of the line <c>text[start..end)</c> with rule L1 applied: segment and paragraph
    /// separators, and whitespace and isolate formatting characters before them or at the end of
    /// the line, go back to the paragraph level, so trailing spaces sit at the paragraph's end
    /// rather than in the middle of a right-to-left run.
    /// </summary>
    public byte[] GetLineLevels(int start, int end)
    {
        CheckLine(start, end);
        var levels = _levels.AsSpan(start, end - start).ToArray();
        var trailing = true;
        for (var i = end - 1; i >= start; i--)
        {
            var c = _classes[i];
            if (c is BidiClass.B or BidiClass.S)
            {
                levels[i - start] = BaseLevel;
                trailing = true;
            }
            else if (trailing && IsWhitespaceForL1(c))
            {
                levels[i - start] = BaseLevel;
            }
            else
            {
                trailing = false;
            }
        }
        if (_text is not null)
        {
            // L1 sees the low half of a surrogate pair as BN; it follows its high half instead.
            for (var i = start + 1; i < end; i++)
            {
                if (char.IsLowSurrogate(_text[i]) && char.IsHighSurrogate(_text[i - 1]))
                {
                    levels[i - start] = levels[i - 1 - start];
                }
            }
        }
        return levels;
    }

    /// <summary>
    /// The display order of the line <c>text[start..end)</c> (rules L1 and L2):
    /// <c>result[visualIndex]</c> is the logical UTF-16 index into the whole text, left to right.
    /// A surrogate pair keeps its two halves in logical order.
    /// </summary>
    public int[] GetVisualOrder(int start, int end)
    {
        var order = ReorderLevels(GetLineLevels(start, end));
        for (var v = 0; v < order.Length; v++)
        {
            order[v] += start;
        }
        if (_text is not null)
        {
            for (var v = 0; v + 1 < order.Length; v++)
            {
                var i = order[v];
                if (order[v + 1] == i - 1 && char.IsLowSurrogate(_text[i]) && char.IsHighSurrogate(_text[i - 1]))
                {
                    (order[v], order[v + 1]) = (i - 1, i);
                    v++;
                }
            }
        }
        return order;
    }

    /// <summary>
    /// Rule L2 on any levels: from the highest level down to the lowest odd level, reverses every
    /// run at that level or above. <c>result[visualIndex]</c> is the index into
    /// <paramref name="levels"/>.
    /// </summary>
    public static int[] ReorderLevels(ReadOnlySpan<byte> levels)
    {
        var order = new int[levels.Length];
        byte highest = 0;
        byte lowestOdd = byte.MaxValue;
        for (var i = 0; i < levels.Length; i++)
        {
            order[i] = i;
            var level = levels[i];
            highest = Math.Max(highest, level);
            if ((level & 1) == 1)
            {
                lowestOdd = Math.Min(lowestOdd, level);
            }
        }

        // Runs at or above a level only ever reverse within runs at or above every lower level, so
        // the logical levels still mark where each run is after the higher passes.
        for (var level = highest; level >= lowestOdd; level--)
        {
            var i = 0;
            while (i < levels.Length)
            {
                if (levels[i] < level)
                {
                    i++;
                    continue;
                }
                var end = i + 1;
                while (end < levels.Length && levels[end] >= level)
                {
                    end++;
                }
                order.AsSpan(i, end - i).Reverse();
                i = end;
            }
        }
        return order;
    }

    /// <summary>
    /// Rule L4: whether the character at a UTF-16 index is drawn mirrored — it has the
    /// Bidi_Mirrored property (parentheses, brackets, less-than, …) and resolves right to left.
    /// Choosing the mirrored glyph is the shaper's job.
    /// </summary>
    public bool IsMirrored(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _levels.Length);
        return _text is not null
            && (_levels[index] & 1) == 1
            && BidiMirroredData.Get(BidiResolver.CodePointAt(_text, index)) == BidiMirrored.Yes;
    }

    private static bool IsWhitespaceForL1(BidiClass c) =>
        c is BidiClass.WS or BidiClass.FSI or BidiClass.LRI or BidiClass.RLI or BidiClass.PDI
        || BidiResolver.IsRemovedByX9(c);

    private void CheckLine(int start, int end)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, _levels.Length);
    }
}
