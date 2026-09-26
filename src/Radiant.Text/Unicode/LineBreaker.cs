using System;
using System.Collections.Generic;
using static Radiant.Text.Unicode.LineBreakClass;

namespace Radiant.Text.Unicode;

/// <summary>
/// Finds where text may be broken into lines, by the Unicode Line Breaking Algorithm (UAX #14,
/// rules LB1–LB31 as of Unicode 16.0, untailored). Paragraph layout fills a line up to the last
/// opportunity that fits; without these, it would split words, strand punctuation at the start of
/// a line, or pull apart numbers like "$(12.35)" and emoji sequences.
/// </summary>
/// <remarks>
/// One forward pass over the code points. The rules mostly look at the pair either side of a
/// position; the few that look further (spaces between an opening bracket and what follows,
/// numbers, quotation marks, regional-indicator pairs, Brahmic orthographic syllables) are served
/// by a little state carried forward and, rarely, a peek at the next one or two characters.
/// Combining marks are folded into their base as LB9 says, so the state only ever describes base
/// characters. Nothing is allocated but the result.
/// </remarks>
public static class LineBreaker
{
    // Markers for the start and end of text in lookback and lookahead; not real Line_Break values.
    private const LineBreakClass Sot = (LineBreakClass)0xFE;
    private const LineBreakClass Eot = (LineBreakClass)0xFF;
    private const int LatinCapitalA = 0x41;
    private const int Hyphen = 0x2010;
    private const int DottedCircle = 0x25CC;

    /// <summary>
    /// Every position (a UTF-16 index into <paramref name="text"/>) before which a line may break,
    /// in order. Mandatory breaks follow hard line breaks (BK, CR, LF, NL; CR LF is one) and end the
    /// text. Index 0 is never included; <c>text.Length</c> always is, as mandatory, unless the text
    /// is empty, which has no opportunities at all.
    /// </summary>
    public static IReadOnlyList<LineBreakOpportunity> GetOpportunities(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var breaks = new List<LineBreakOpportunity>();
        if (text.Length == 0)
        {
            return breaks;
        }

        // The previous character exactly as it is (for the rules before LB9), and the previous
        // base character with its combining marks folded in (for LB9 onwards), and the one before.
        var cp = CodePointAt(text, 0);
        var width = cp > 0xFFFF ? 2 : 1;
        var raw = Resolve(cp);
        var cls = raw;
        if (cls is CM or ZWJ)
        {
            // LB10: a mark with no base stands alone as a letter.
            cls = AL;
            cp = LatinCapitalA;
        }
        var prevActual = raw;
        var prev = cls;
        var prevCp = cp;
        var prevPrev = Sot;
        var prevPrevCp = -1;

        // The last base character that was not a space: "X SP*" rules look through spaces to it.
        var lastNonSpace = cls == SP ? Sot : cls;
        // LB15a: that character is an initial quotation mark at the start of a quote.
        var opensQuote = cls == QU && IsInitialPunctuation(cp);
        // LB25: the text so far ends NU (SY | IS)*, or NU (SY | IS)* (CL | CP).
        var inNumber = cls == NU;
        var closesNumber = false;
        // LB30a: how many regional indicators in a row end the text so far.
        var regionalIndicators = cls == RI ? 1 : 0;

        for (var i = width; i < text.Length; i += width)
        {
            cp = CodePointAt(text, i);
            width = cp > 0xFFFF ? 2 : 1;
            raw = Resolve(cp);

            // LB9: marks and joiners belong to the character they follow, unless that is a space
            // or a line break. (No earlier rule can break here: they all need a different pair.)
            if (raw is CM or ZWJ && prev is not (BK or CR or LF or NL or SP or ZW))
            {
                prevActual = raw;
                continue;
            }

            cls = raw;
            if (cls is CM or ZWJ)
            {
                // LB10: treat as U+0041 in every property.
                cls = AL;
                cp = LatinCapitalA;
            }

            if (prevActual is BK or LF or NL || (prevActual == CR && raw != LF))
            {
                breaks.Add(new LineBreakOpportunity(i, true)); // LB4, LB5
            }
            else if (BreakAllowed(i + width))
            {
                breaks.Add(new LineBreakOpportunity(i, false));
            }

            if (cls != SP)
            {
                opensQuote = cls == QU && IsInitialPunctuation(cp)
                    && prev is Sot or BK or CR or LF or NL or OP or QU or GL or SP or ZW;
                lastNonSpace = cls;
            }
            closesNumber = inNumber && cls is CL or CP;
            inNumber = cls == NU || (inNumber && cls is SY or IS);
            regionalIndicators = cls == RI ? regionalIndicators + 1 : 0;
            prevPrev = prev;
            prevPrevCp = prevCp;
            prev = cls;
            prevCp = cp;
            prevActual = raw;
        }

        breaks.Add(new LineBreakOpportunity(text.Length, true)); // LB3
        return breaks;

        // LB5 (CR × LF) to LB31 for the position before cls/cp, the first rule that matches
        // deciding. Mandatory breaks have already been taken; LB2 is the loop starting at 1.
        bool BreakAllowed(int next)
        {
            if (prevActual == CR || cls is BK or CR or LF or NL or SP or ZW)
            {
                return false; // LB5 CR × LF, LB6, LB7
            }
            if (lastNonSpace == ZW)
            {
                return true; // LB8
            }
            if (prevActual == ZWJ)
            {
                return false; // LB8a
            }
            if (cls == WJ || prev is WJ or GL || (cls == GL && prev is not (SP or BA or HY)))
            {
                return false; // LB11, LB12, LB12a
            }
            if (cls is CL or CP or EX or SY || lastNonSpace == OP || opensQuote)
            {
                return false; // LB13, LB14, LB15a
            }
            if (cls == QU && IsFinalPunctuation(cp)
                && Peek(text, next).Class is SP or GL or WJ or CL or QU or CP or EX or IS or SY or BK or CR or LF or NL or ZW or Eot)
            {
                return false; // LB15b
            }
            if (cls == IS)
            {
                return prev == SP && Peek(text, next).Class == NU; // LB15c, LB15d
            }
            if ((lastNonSpace is CL or CP && cls == NS) || (lastNonSpace == B2 && cls == B2))
            {
                return false; // LB16, LB17
            }
            if (prev == SP)
            {
                return true; // LB18
            }
            if (cls == QU)
            {
                // LB19, then LB19a: only an East Asian context allows a break before a quote.
                if (!IsInitialPunctuation(cp) || !IsEastAsian(prevCp))
                {
                    return false;
                }
                var after = Peek(text, next);
                if (after.Class == Eot || !IsEastAsian(after.CodePoint))
                {
                    return false;
                }
            }
            if (prev == QU && (!IsFinalPunctuation(prevCp) || !IsEastAsian(cp) || prevPrev == Sot || !IsEastAsian(prevPrevCp)))
            {
                return false; // LB19, LB19a
            }
            if (cls == CB || prev == CB)
            {
                return true; // LB20
            }
            if (cls == AL && (prev == HY || prevCp == Hyphen) && prevPrev is Sot or BK or CR or LF or NL or SP or ZW or CB or GL)
            {
                return false; // LB20a
            }
            if (cls is BA or HY or NS || prev == BB)
            {
                return false; // LB21
            }
            if (prevPrev == HL && (prev == HY || (prev == BA && !IsEastAsian(prevCp))) && cls != HL)
            {
                return false; // LB21a
            }
            if ((prev == SY && cls == HL) || cls == IN)
            {
                return false; // LB21b, LB22
            }
            if ((prev is AL or HL && cls == NU) || (prev == NU && cls is AL or HL))
            {
                return false; // LB23
            }
            if ((prev == PR && cls is ID or EB or EM) || (prev is ID or EB or EM && cls == PO))
            {
                return false; // LB23a
            }
            if ((prev is PR or PO && cls is AL or HL) || (prev is AL or HL && cls is PR or PO))
            {
                return false; // LB24
            }
            if ((cls is PO or PR && (inNumber || closesNumber)) || (cls == NU && (inNumber || prev is PO or PR or HY or IS)))
            {
                return false; // LB25
            }
            if (prev is PO or PR && cls == OP)
            {
                var after = Peek(text, next);
                if (after.Class == NU || (after.Class == IS && Peek(text, after.Next).Class == NU))
                {
                    return false; // LB25
                }
            }
            if ((prev == JL && cls is JL or JV or H2 or H3) || (prev is JV or H2 && cls is JV or JT) || (prev is JT or H3 && cls == JT))
            {
                return false; // LB26
            }
            if ((prev is JL or JV or JT or H2 or H3 && cls == PO) || (prev == PR && cls is JL or JV or JT or H2 or H3))
            {
                return false; // LB27
            }
            if (prev is AL or HL && cls is AL or HL)
            {
                return false; // LB28
            }
            if (IsAksara(prev, prevCp))
            {
                if (cls is VF or VI || (IsAksara(cls, cp) && Peek(text, next).Class == VF))
                {
                    return false; // LB28a
                }
            }
            else if ((prev == AP && IsAksara(cls, cp)) || (prev == VI && IsAksara(prevPrev, prevPrevCp) && (cls == AK || cp == DottedCircle)))
            {
                return false; // LB28a
            }
            if (prev == IS && cls is AL or HL)
            {
                return false; // LB29
            }
            if ((prev is AL or HL or NU && cls == OP && !IsEastAsian(cp)) || (prev == CP && cls is AL or HL or NU && !IsEastAsian(prevCp)))
            {
                return false; // LB30
            }
            if (prev == RI && cls == RI && regionalIndicators % 2 == 1)
            {
                return false; // LB30a
            }
            if (cls == EM && (prev == EB || IsUnassignedPictographic(prevCp)))
            {
                return false; // LB30b
            }
            return true; // LB31
        }
    }

    /// <summary>
    /// The next base character at or after <paramref name="index"/>, skipping the marks that LB9
    /// folds into the character before it, and the index after it.
    /// </summary>
    private static (LineBreakClass Class, int CodePoint, int Next) Peek(string text, int index)
    {
        while (index < text.Length)
        {
            var cp = CodePointAt(text, index);
            var cls = Resolve(cp);
            index += cp > 0xFFFF ? 2 : 1;
            if (cls is not (CM or ZWJ))
            {
                return (cls, cp, index);
            }
        }
        return (Eot, -1, index);
    }

    /// <summary>
    /// LB1: the Line_Break class of a code point, with the classes that depend on context outside
    /// this algorithm resolved as UAX #14 recommends when no such context is known.
    /// </summary>
    private static LineBreakClass Resolve(int codePoint)
    {
        var cls = LineBreakData.Get(codePoint);
        return cls switch
        {
            AI or SG or XX => AL,
            SA => GeneralCategoryData.Get(codePoint) is GeneralCategory.Mn or GeneralCategory.Mc ? CM : AL,
            CJ => NS,
            _ => cls,
        };
    }

    /// <summary>A whole code point at <paramref name="index"/>; a lone surrogate stands for itself.</summary>
    private static int CodePointAt(string text, int index)
    {
        var c = text[index];
        if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            return char.ConvertToUtf32(c, text[index + 1]);
        }
        return c;
    }

    /// <summary>$EastAsian in UAX #14: East_Asian_Width Fullwidth, Wide or Halfwidth.</summary>
    private static bool IsEastAsian(int codePoint) =>
        codePoint >= 0 && EastAsianWidthData.Get(codePoint) != EastAsianWidth.N;

    private static bool IsInitialPunctuation(int codePoint) => GeneralCategoryData.Get(codePoint) == GeneralCategory.Pi;

    private static bool IsFinalPunctuation(int codePoint) => GeneralCategoryData.Get(codePoint) == GeneralCategory.Pf;

    /// <summary>LB30b's [\p{Extended_Pictographic}&amp;\p{Cn}]: emoji-to-be, not yet assigned.</summary>
    private static bool IsUnassignedPictographic(int codePoint) =>
        codePoint >= 0
        && ExtendedPictographicData.Get(codePoint) == ExtendedPictographic.Yes
        && GeneralCategoryData.Get(codePoint) == GeneralCategory.Cn;

    /// <summary>LB28a's (AK | [◌] | AS): a Brahmic aksara, or a dotted circle standing in for one.</summary>
    private static bool IsAksara(LineBreakClass cls, int codePoint) => cls is AK or AS || codePoint == DottedCircle;
}
