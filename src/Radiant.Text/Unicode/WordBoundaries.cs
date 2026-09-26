using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Wb = Radiant.Text.Unicode.WordBreakProperty;

namespace Radiant.Text.Unicode;

/// <summary>
/// Word boundaries as Unicode defines them (UAX #29, rules WB1–WB999), for moving the caret by
/// words and selecting a word on double-click. "can't", "3.14" and "1,000" stay whole, emoji ZWJ
/// sequences and flags are never split, and spaces and punctuation form segments of their own,
/// which <see cref="IsWord"/> tells apart from words.
/// </summary>
public static class WordBoundaries
{
    /// <summary>
    /// Every word boundary in <paramref name="text"/> as UTF-16 indices, in order, including 0 and
    /// <c>text.Length</c>; empty text has the single boundary 0.
    /// </summary>
    public static IReadOnlyList<int> Get(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Compute(text);
    }

    /// <summary>The nearest word boundary strictly before <paramref name="index"/>, or 0.</summary>
    public static int Previous(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);
        return BoundarySearch.Previous(Compute(text), index);
    }

    /// <summary>The nearest word boundary strictly after <paramref name="index"/>, or <c>text.Length</c>.</summary>
    public static int Next(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);
        return BoundarySearch.Next(Compute(text), index);
    }

    /// <summary>
    /// Whether the segment <c>text[start..end)</c> is a word: it holds a letter (which includes
    /// ideographs and kana) or a number, rather than only spaces, punctuation or symbols. Moving by
    /// words skips the segments that aren't.
    /// </summary>
    public static bool IsWord(string text, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, text.Length);
        foreach (var rune in text.AsSpan(start, end - start).EnumerateRunes())
        {
            switch (Rune.GetUnicodeCategory(rune))
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.LetterNumber:
                case UnicodeCategory.OtherNumber:
                    return true;
            }
        }
        return false;
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
        var classes = new Wb[count];
        var pictographic = new bool[count];
        for (var i = 0; i < count; i++)
        {
            classes[i] = WordBreakData.Get(codePoints[i]);
            pictographic[i] = ExtendedPictographicData.Get(codePoints[i]) == ExtendedPictographicValue.Yes;
        }
        for (var i = 1; i < count; i++)
        {
            if (Breaks(classes, pictographic, i))
            {
                boundaries.Add(starts[i]);
            }
        }
        boundaries.Add(text.Length);
        return boundaries;
    }

    /// <summary>Whether there is a word boundary between code points <c>i - 1</c> and <c>i</c>.</summary>
    private static bool Breaks(Wb[] classes, bool[] pictographic, int i)
    {
        var before = classes[i - 1];
        var after = classes[i];

        // WB3–WB3d look at the code points either side, before WB4 hides anything.
        if (before == Wb.CR && after == Wb.LF)
        {
            return false;
        }
        if (IsNewline(before) || IsNewline(after))
        {
            return true;
        }
        if (before == Wb.ZWJ && pictographic[i])
        {
            return false;
        }
        if (before == Wb.WSegSpace && after == Wb.WSegSpace)
        {
            return false;
        }

        // WB4: Extend, Format and ZWJ join what precedes them, and the rules below skip them.
        if (IsIgnored(after))
        {
            return false;
        }
        var previousIndex = Absorber(classes, i - 1);
        var previous = classes[previousIndex];
        var beforePrevious = previousIndex > 0 ? classes[Absorber(classes, previousIndex - 1)] : Wb.XX;
        var nextIndex = i + 1;
        while (nextIndex < classes.Length && IsIgnored(classes[nextIndex]))
        {
            nextIndex++;
        }
        var next = nextIndex < classes.Length ? classes[nextIndex] : Wb.XX;

        // WB5–WB7: letters, and letters joined by apostrophes and the like.
        if (IsAHLetter(previous) && IsAHLetter(after))
        {
            return false;
        }
        if (IsAHLetter(previous) && IsMidLetterish(after) && IsAHLetter(next))
        {
            return false;
        }
        if (IsAHLetter(beforePrevious) && IsMidLetterish(previous) && IsAHLetter(after))
        {
            return false;
        }

        // WB7a–WB7c: Hebrew letters and quotes (geresh and gershayim).
        if (previous == Wb.HL && after == Wb.SQ)
        {
            return false;
        }
        if (previous == Wb.HL && after == Wb.DQ && next == Wb.HL)
        {
            return false;
        }
        if (beforePrevious == Wb.HL && previous == Wb.DQ && after == Wb.HL)
        {
            return false;
        }

        // WB8–WB12: numbers, letters with numbers, and numbers with separators.
        if ((previous == Wb.NU || IsAHLetter(previous)) && (after == Wb.NU || IsAHLetter(after)))
        {
            return false;
        }
        if (beforePrevious == Wb.NU && IsMidNumish(previous) && after == Wb.NU)
        {
            return false;
        }
        if (previous == Wb.NU && IsMidNumish(after) && next == Wb.NU)
        {
            return false;
        }

        // WB13–WB13b: katakana, and connectors such as the underscore.
        if (previous == Wb.KA && after == Wb.KA)
        {
            return false;
        }
        if ((IsAHLetter(previous) || previous is Wb.NU or Wb.KA or Wb.EX) && after == Wb.EX)
        {
            return false;
        }
        if (previous == Wb.EX && (IsAHLetter(after) || after is Wb.NU or Wb.KA))
        {
            return false;
        }

        // WB15–WB16: regional indicators pair up into flags.
        if (previous == Wb.RI && after == Wb.RI)
        {
            var run = 0;
            var j = previousIndex;
            while (classes[j] == Wb.RI)
            {
                run++;
                if (j == 0)
                {
                    break;
                }
                j = Absorber(classes, j - 1);
            }
            return run % 2 == 0;
        }

        // WB999.
        return true;
    }

    /// <summary>
    /// The index of the code point that the one at <paramref name="index"/> belongs to under WB4:
    /// itself, or the nearest earlier code point that isn't Extend, Format or ZWJ. Those attach to
    /// anything but the start of text or a line break, where they stand alone.
    /// </summary>
    private static int Absorber(Wb[] classes, int index)
    {
        while (index > 0 && IsIgnored(classes[index]) && !IsNewline(classes[index - 1]))
        {
            index--;
        }
        return index;
    }

    private static bool IsNewline(Wb value) => value is Wb.CR or Wb.LF or Wb.NL;

    private static bool IsIgnored(Wb value) => value is Wb.Extend or Wb.FO or Wb.ZWJ;

    private static bool IsAHLetter(Wb value) => value is Wb.LE or Wb.HL;

    // (MidLetter | MidNumLetQ), where MidNumLetQ is MidNumLet or Single_Quote.
    private static bool IsMidLetterish(Wb value) => value is Wb.ML or Wb.MB or Wb.SQ;

    // (MidNum | MidNumLetQ).
    private static bool IsMidNumish(Wb value) => value is Wb.MN or Wb.MB or Wb.SQ;
}
