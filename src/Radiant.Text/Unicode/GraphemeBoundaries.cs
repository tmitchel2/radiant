using System;
using System.Collections.Generic;
using Gcb = Radiant.Text.Unicode.GraphemeBreakProperty;
using InCB = Radiant.Text.Unicode.IndicConjunctBreak;

namespace Radiant.Text.Unicode;

/// <summary>
/// Extended grapheme cluster boundaries as Unicode defines them (UAX #29, rules GB1–GB999): the
/// user-perceived characters that the caret steps over and backspace deletes whole. An accented
/// letter, a Hangul syllable, an emoji ZWJ sequence, a flag and a Devanagari conjunct are each one
/// cluster. .NET's <see cref="System.Globalization.StringInfo"/> is not used because it splits
/// Indic conjuncts (GB9c), failing Unicode 16's conformance test.
/// </summary>
public static class GraphemeBoundaries
{
    /// <summary>
    /// Every grapheme cluster boundary in <paramref name="text"/> as UTF-16 indices, in order,
    /// including 0 and <c>text.Length</c>; empty text has the single boundary 0.
    /// </summary>
    public static IReadOnlyList<int> Get(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Compute(text);
    }

    /// <summary>The nearest grapheme cluster boundary strictly before <paramref name="index"/>, or 0.</summary>
    public static int Previous(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);
        return BoundarySearch.Previous(Compute(text), index);
    }

    /// <summary>The nearest grapheme cluster boundary strictly after <paramref name="index"/>, or <c>text.Length</c>.</summary>
    public static int Next(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);
        return BoundarySearch.Next(Compute(text), index);
    }

    private static List<int> Compute(string text)
    {
        var boundaries = new List<int> { 0 };
        if (text.Length == 0)
        {
            return boundaries;
        }
        var codePoints = new int[text.Length];
        var starts = new int[text.Length];
        var count = CodePoints.Decode(text, codePoints, starts);

        // What precedes the current position, for the rules that look back further than one code
        // point: an emoji then Extend* then ZWJ (GB11), a run of regional indicators (GB12–GB13),
        // and a consonant then Extend/Linker with at least one Linker (GB9c).
        var emoji = EmojiState.None;
        var regionalIndicators = 0;
        var conjunct = ConjunctState.None;

        var before = GraphemeBreakData.Get(codePoints[0]);
        var beforeInCB = IndicConjunctBreakData.Get(codePoints[0]);
        var beforePictographic = IsPictographic(codePoints[0]);
        for (var i = 1; i < count; i++)
        {
            emoji = beforePictographic ? EmojiState.Pictographic
                : emoji == EmojiState.Pictographic && before == Gcb.EX ? EmojiState.Pictographic
                : emoji == EmojiState.Pictographic && before == Gcb.ZWJ ? EmojiState.Joined
                : EmojiState.None;
            regionalIndicators = before == Gcb.RI ? regionalIndicators + 1 : 0;
            conjunct = beforeInCB == InCB.Consonant ? ConjunctState.Consonant
                : conjunct != ConjunctState.None && beforeInCB == InCB.Linker ? ConjunctState.Linked
                : beforeInCB == InCB.Extend ? conjunct
                : ConjunctState.None;

            var after = GraphemeBreakData.Get(codePoints[i]);
            var afterInCB = IndicConjunctBreakData.Get(codePoints[i]);
            var afterPictographic = IsPictographic(codePoints[i]);
            if (Breaks(before, after, afterInCB == InCB.Consonant && conjunct == ConjunctState.Linked,
                afterPictographic && emoji == EmojiState.Joined, regionalIndicators))
            {
                boundaries.Add(starts[i]);
            }
            before = after;
            beforeInCB = afterInCB;
            beforePictographic = afterPictographic;
        }
        boundaries.Add(text.Length);
        return boundaries;
    }

    /// <summary>
    /// Whether there is a boundary between two code points, given whether GB9c's conjunct and
    /// GB11's emoji sequence continue across it and how many regional indicators precede it.
    /// </summary>
    private static bool Breaks(Gcb before, Gcb after, bool conjunctContinues, bool emojiContinues, int regionalIndicators)
    {
        if (before == Gcb.CR && after == Gcb.LF)
        {
            return false; // GB3
        }
        if (before is Gcb.CN or Gcb.CR or Gcb.LF || after is Gcb.CN or Gcb.CR or Gcb.LF)
        {
            return true; // GB4, GB5
        }
        if (before == Gcb.L && after is Gcb.L or Gcb.V or Gcb.LV or Gcb.LVT)
        {
            return false; // GB6
        }
        if (before is Gcb.LV or Gcb.V && after is Gcb.V or Gcb.T)
        {
            return false; // GB7
        }
        if (before is Gcb.LVT or Gcb.T && after == Gcb.T)
        {
            return false; // GB8
        }
        if (after is Gcb.EX or Gcb.ZWJ or Gcb.SM || before == Gcb.PP)
        {
            return false; // GB9, GB9a, GB9b
        }
        if (conjunctContinues || emojiContinues)
        {
            return false; // GB9c, GB11
        }
        if (before == Gcb.RI && after == Gcb.RI)
        {
            return regionalIndicators % 2 == 0; // GB12, GB13
        }
        return true; // GB999
    }

    private static bool IsPictographic(int codePoint) =>
        ExtendedPictographicData.Get(codePoint) == ExtendedPictographicValue.Yes;

    private enum EmojiState
    {
        None,
        Pictographic,
        Joined,
    }

    private enum ConjunctState
    {
        None,
        Consonant,
        Linked,
    }
}
