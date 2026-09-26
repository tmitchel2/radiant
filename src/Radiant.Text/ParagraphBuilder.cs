using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using HarfBuzzSharp;
using Radiant.Text.Unicode;

namespace Radiant.Text;

/// <summary>
/// Paragraph layout, in two stages. The constructor does the work that doesn't depend on width,
/// once: bidi levels, splitting into runs (by style, font, script and level), shaping, and each
/// character's advance. <see cref="Layout"/> then breaks lines at a width and places glyphs, so
/// laying the same text out at several widths reshapes only the pieces of runs a line break cuts.
/// </summary>
internal sealed class ParagraphBuilder
{
    // Slack for float error when comparing a width with the width it must fit.
    private const float Epsilon = 1e-3f;

    private readonly string _text;
    private readonly List<BidiSection> _sections = [];
    private readonly List<TextRun> _runs = [];
    private readonly byte[] _levels;
    private readonly IReadOnlyList<LineBreakOpportunity> _breaks;
    private readonly float[] _prefix;
    private readonly Dictionary<(string Family, bool Italic), FontFace> _faces = [];
    private readonly Dictionary<(FontFace Face, float Weight, float Size, IReadOnlyList<FontVariation> Variations), FontInstance> _instances = [];
    private int[]? _words;

    public ParagraphBuilder(AttributedText text, ParagraphStyle style, FontLibrary fonts)
    {
        Text = text;
        Style = style;
        Fonts = fonts;
        _text = text.Text;
        _levels = new byte[_text.Length];
        ResolveBidi();
        Graphemes = [.. GraphemeBoundaries.Get(_text)];
        _breaks = LineBreaker.GetOpportunities(_text);
        Itemize();

        var advances = new float[_text.Length];
        foreach (var run in _runs)
        {
            var section = _sections[run.Section];
            var shaped = Shape(run, run.Start, run.End, run.Level, section.Start, section.End);
            run.Shaped = shaped;
            for (var i = 0; i < shaped.Count; i++)
            {
                advances[shaped.Clusters[i]] += shaped.Advances[i];
            }
        }
        _prefix = new float[_text.Length + 1];
        for (var i = 0; i < advances.Length; i++)
        {
            _prefix[i + 1] = _prefix[i] + advances[i];
        }
        (MinIntrinsicWidth, MaxIntrinsicWidth) = IntrinsicWidths();
    }

    public AttributedText Text { get; }

    public ParagraphStyle Style { get; }

    public FontLibrary Fonts { get; }

    /// <summary>Grapheme boundaries, 0 and the length included.</summary>
    public int[] Graphemes { get; }

    /// <summary>Word boundaries, 0 and the length included; found the first time they're asked for.</summary>
    public int[] Words => _words ??= [.. WordBoundaries.Get(_text)];

    /// <summary>The widest the paragraph can be made to shrink without breaking inside a word.</summary>
    public float MinIntrinsicWidth { get; }

    /// <summary>The width the paragraph takes with no wrapping: its widest hard line.</summary>
    public float MaxIntrinsicWidth { get; }

    /// <summary>Breaks lines at <paramref name="maxWidth"/> and places every glyph.</summary>
    public Paragraph Layout(float maxWidth)
    {
        var ranges = BreakLines(maxWidth, out var exceeded);
        var measured = new MeasuredLine[ranges.Count];
        var longest = 0f;
        for (var i = 0; i < ranges.Count; i++)
        {
            measured[i] = Measure(ranges[i]);
            longest = MathF.Max(longest, measured[i].ContentWidth);
        }

        var width = float.IsFinite(maxWidth) ? maxWidth : longest;
        var lines = new TextLine[measured.Length];
        var top = 0f;
        for (var i = 0; i < measured.Length; i++)
        {
            lines[i] = Place(i, ranges[i], measured[i], width, top);
            top += lines[i].Height;
        }
        return new Paragraph(this, Style with { MaxWidth = maxWidth }, lines, width, top, longest, exceeded);
    }

    /// <summary>The bidi paragraph the index is in, or -1 in text that has none (empty text).</summary>
    public int SectionAt(int index)
    {
        int lo = 0, hi = _sections.Count - 1;
        if (hi < 0)
        {
            return -1;
        }
        while (lo < hi)
        {
            var mid = (lo + hi + 1) >>> 1;
            if (_sections[mid].Start <= index)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return lo;
    }

    /// <summary>
    /// Whether a character hangs at the end of a line: spaces (not no-break ones) and line breaks.
    /// Hanging characters don't count towards whether a line fits or where it aligns.
    /// </summary>
    internal static bool IsHanging(char c) =>
        c is ' ' or '\t' or '\u1680' or '\u205F' or '\u3000'
        || (c >= '\u2000' && c <= '\u200A' && c != '\u2007')
        || IsLineBreak(c);

    /// <summary>Whether a character ends a line: the mandatory breaks of UAX #14.</summary>
    internal static bool IsLineBreak(char c) =>
        c is '\n' or '\r' or '\u000B' or '\u000C' or '\u0085' or '\u2028' or '\u2029';

    /// <summary>How many code units before <paramref name="end"/> (and not before <paramref name="start"/>) are a line break.</summary>
    internal int LineBreakLengthBefore(int end, int start)
    {
        if (end <= start || !IsLineBreak(_text[end - 1]))
        {
            return 0;
        }
        return _text[end - 1] == '\n' && end - 2 >= start && _text[end - 2] == '\r' ? 2 : 1;
    }

    // --- Width-independent work ---

    private void ResolveBidi()
    {
        var start = 0;
        for (var i = 0; i < _text.Length; i++)
        {
            var c = _text[i];
            var endsParagraph = c is '\n' or '\u0085' or '\u2029'
                || (c == '\r' && (i + 1 == _text.Length || _text[i + 1] != '\n'));
            if (endsParagraph)
            {
                AddSection(start, i + 1);
                start = i + 1;
            }
        }
        if (start < _text.Length)
        {
            AddSection(start, _text.Length);
        }
    }

    private void AddSection(int start, int end)
    {
        var bidi = BidiParagraph.Resolve(_text[start..end], Style.Direction);
        for (var i = 0; i < bidi.Levels.Count; i++)
        {
            _levels[start + i] = bidi.Levels[i];
        }
        _sections.Add(new BidiSection(start, end, bidi));
    }

    /// <summary>
    /// Splits the text into runs that each shape as one piece. Characters with no script of their
    /// own (spaces, digits, punctuation, marks) join the run they're in, and stay in its font when
    /// it has them, so a space between two Arabic words doesn't split an Arabic fallback run.
    /// Fonts are chosen per grapheme, by its first character, so a mark stays with its base.
    /// </summary>
    private void Itemize()
    {
        var spans = Text.Spans;
        var spanIndex = 0;
        var section = 0;
        TextRun? run = null;
        for (var g = 0; g + 1 < Graphemes.Length; g++)
        {
            var start = Graphemes[g];
            var end = Graphemes[g + 1];
            while (spans[spanIndex].End <= start)
            {
                spanIndex++;
            }
            while (_sections[section].End <= start)
            {
                section++;
            }

            var style = spans[spanIndex].Style;
            var codePoint = CodePointAt(start);
            var level = _levels[start];
            var script = UnicodeFunctions.Default.GetScript(codePoint);
            var common = IsCommon(script);
            var sameStyle = run is not null && run.Style == style;
            var face = sameStyle && (char.IsControl(_text[start]) || (common && run!.Face.HasGlyph(codePoint)))
                ? run!.Face
                : Fonts.FaceFor(codePoint, PreferredFace(style));

            if (sameStyle && run!.Section == section && run.Level == level && ReferenceEquals(run.Face, face)
                && (common || IsCommon(run.Script) || run.Script == script))
            {
                run.End = end;
                if (!common && IsCommon(run.Script))
                {
                    run.Script = script;
                }
            }
            else
            {
                run = new TextRun(start, end, style, face, level, section, script);
                _runs.Add(run);
            }
        }
    }

    private static bool IsCommon(Script script) =>
        script == Script.Common || script == Script.Inherited || script == Script.Unknown;

    private int CodePointAt(int index) =>
        Rune.DecodeFromUtf16(_text.AsSpan(index), out var rune, out _) == System.Buffers.OperationStatus.Done
            ? rune.Value
            : Rune.ReplacementChar.Value;

    private FontFace PreferredFace(TextStyle style)
    {
        var key = (style.FontFamily, style.Italic);
        if (!_faces.TryGetValue(key, out var face))
        {
            face = Fonts.ResolveFace(style.FontFamily, style.Italic);
            _faces[key] = face;
        }
        return face;
    }

    private FontInstance Instance(FontFace face, TextStyle style)
    {
        var key = (face, style.Weight, style.Size, style.Variations);
        if (!_instances.TryGetValue(key, out var instance))
        {
            FontVariation[] variations =
            [
                new FontVariation(FontVariation.Weight, style.Weight),
                new FontVariation(FontVariation.OpticalSize, style.Size),
                .. style.Variations,
            ];
            instance = face.Instance(variations);
            _instances[key] = instance;
        }
        return instance;
    }

    private ShapeOptions Options(TextStyle style, byte level) => new()
    {
        Direction = (level & 1) == 0 ? TextDirection.LeftToRight : TextDirection.RightToLeft,
        Language = style.Language,
        Features = style.Features,
        Tracking = style.Tracking,
        TabSize = Style.TabSize,
    };

    private ShapedRun Shape(TextRun run, int start, int end, byte level, int contextStart, int contextEnd) =>
        TextShaper.Shape(_text, start, end - start, contextStart, contextEnd,
            Instance(run.Face, run.Style), run.Style.Size, Options(run.Style, level));

    private (float Min, float Max) IntrinsicWidths()
    {
        float min = 0f, max = 0f;
        int wordStart = 0, lineStart = 0;
        foreach (var opportunity in _breaks)
        {
            min = MathF.Max(min, ContentWidth(wordStart, opportunity.Index));
            wordStart = opportunity.Index;
            if (opportunity.IsMandatory)
            {
                max = MathF.Max(max, ContentWidth(lineStart, opportunity.Index));
                lineStart = opportunity.Index;
            }
        }
        return (min, max);
    }

    // --- Line breaking ---

    private int TrimEnd(int start, int end)
    {
        while (end > start && IsHanging(_text[end - 1]))
        {
            end--;
        }
        return end;
    }

    /// <summary>The width of <c>text[start..end)</c> as first shaped, without hanging spaces at its end.</summary>
    private float ContentWidth(int start, int end) => _prefix[TrimEnd(start, end)] - _prefix[start];

    private bool Fits(int start, int end, float maxWidth) => ContentWidth(start, end) <= maxWidth + Epsilon;

    /// <summary>
    /// Greedy line breaking: each line takes as many break opportunities as fit. A word too wide
    /// for a line of its own is broken between graphemes when <see cref="ParagraphStyle.BreakLongWords"/>
    /// allows, and otherwise overflows.
    /// </summary>
    private List<LineRange> BreakLines(float maxWidth, out bool exceeded)
    {
        var lines = new List<LineRange>();
        var start = 0;
        var lastFit = -1;
        var k = 0;
        while (k < _breaks.Count)
        {
            var opportunity = _breaks[k];
            if (Fits(start, opportunity.Index, maxWidth))
            {
                if (opportunity.IsMandatory)
                {
                    lines.Add(new LineRange(start, opportunity.Index));
                    start = opportunity.Index;
                    lastFit = -1;
                }
                else
                {
                    lastFit = opportunity.Index;
                }
                k++;
                continue;
            }
            if (lastFit > start)
            {
                lines.Add(new LineRange(start, lastFit));
                start = lastFit;
                lastFit = -1;
                continue;
            }
            if (Style.BreakLongWords)
            {
                var cut = FitGraphemes(start, opportunity.Index, maxWidth);
                if (cut < opportunity.Index)
                {
                    lines.Add(new LineRange(start, cut));
                    start = cut;
                    continue;
                }
            }
            lines.Add(new LineRange(start, opportunity.Index));
            start = opportunity.Index;
            lastFit = -1;
            k++;
        }

        // Text that ends with a line break (or is empty) ends with an empty line, for the caret.
        if (_text.Length == 0 || LineBreakLengthBefore(_text.Length, 0) > 0)
        {
            lines.Add(new LineRange(_text.Length, _text.Length));
        }

        exceeded = false;
        if (Style.MaxLines is { } maxLines && lines.Count > Math.Max(1, maxLines))
        {
            exceeded = true;
            Truncate(lines, Math.Max(1, maxLines), maxWidth);
        }
        return lines;
    }

    /// <summary>The furthest grapheme boundary after <paramref name="start"/> that fits, but always at least one grapheme.</summary>
    private int FitGraphemes(int start, int end, float maxWidth)
    {
        var g = BoundaryAfter(Graphemes, start);
        var cut = Graphemes[g];
        for (var i = g + 1; i < Graphemes.Length && Graphemes[i] < end && Fits(start, Graphemes[i], maxWidth); i++)
        {
            cut = Graphemes[i];
        }
        return cut;
    }

    /// <summary>
    /// Keeps the first <paramref name="maxLines"/> lines. With an ellipsis, the last one takes the
    /// rest of its paragraph, cut between graphemes to leave room for the ellipsis at its end.
    /// </summary>
    private void Truncate(List<LineRange> lines, int maxLines, float maxWidth)
    {
        var last = lines[maxLines - 1];
        var end = last.End;
        for (var j = maxLines; LineBreakLengthBefore(end, last.Start) == 0 && j < lines.Count; j++)
        {
            end = lines[j].End;
        }
        lines.RemoveRange(maxLines, lines.Count - maxLines);
        if (Style.Ellipsis is not { Length: > 0 } ellipsisText)
        {
            return;
        }

        end = TrimEnd(last.Start, end);
        var style = Text.StyleAt(Math.Max(last.Start, end - 1));
        var ellipsis = ShapeEllipsis(ellipsisText, style, BaseLevelAt(last.Start));
        var available = maxWidth - ellipsis.Width;
        if (ContentWidth(last.Start, end) > available + Epsilon)
        {
            var cut = last.Start;
            for (var g = BoundaryAfter(Graphemes, last.Start); g < Graphemes.Length && Graphemes[g] <= end; g++)
            {
                if (ContentWidth(last.Start, Graphemes[g]) > available + Epsilon)
                {
                    break;
                }
                cut = Graphemes[g];
            }
            end = TrimEnd(last.Start, cut);
        }
        lines[maxLines - 1] = new LineRange(last.Start, end, ellipsis, style);
    }

    private ShapedRun ShapeEllipsis(string ellipsis, TextStyle style, byte level)
    {
        var codePoint = Rune.GetRuneAt(ellipsis, 0).Value;
        var face = Fonts.FaceFor(codePoint, PreferredFace(style));
        return TextShaper.Shape(ellipsis, 0, ellipsis.Length, 0, ellipsis.Length,
            Instance(face, style), style.Size, Options(style, level));
    }

    private byte BaseLevelAt(int index)
    {
        var section = SectionAt(index);
        if (section >= 0)
        {
            return _sections[section].Bidi.BaseLevel;
        }
        return Style.Direction == TextDirection.RightToLeft ? (byte)1 : (byte)0;
    }

    /// <summary>The index of the first boundary strictly after <paramref name="index"/> (the last if none is).</summary>
    internal static int BoundaryAfter(ReadOnlySpan<int> boundaries, int index)
    {
        int lo = 0, hi = boundaries.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (boundaries[mid] <= index)
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

    // --- Placing lines ---

    /// <summary>A line's pieces in screen order, measured but not yet aligned.</summary>
    private readonly record struct MeasuredLine(
        LineItem[] Items, TextDirection Direction, int LineBreakLength,
        float Width, float ContentWidth, float TrailingWidth, float Ascent, float Descent);

    /// <summary>A piece of a line: part of one run at one level, or the ellipsis.</summary>
    private readonly record struct LineItem(ShapedRun Shaped, TextStyle Style, int Start, int End, byte Level, bool IsEllipsis);

    private MeasuredLine Measure(LineRange range)
    {
        var baseLevel = BaseLevelAt(range.Start);
        var items = new List<LineItem>();
        if (range.End > range.Start)
        {
            var section = _sections[SectionAt(range.Start)];
            var levels = section.Bidi.GetLineLevels(range.Start - section.Start, range.End - section.Start);
            for (var r = RunAt(range.Start); r < _runs.Count && _runs[r].Start < range.End; r++)
            {
                var run = _runs[r];
                var end = Math.Min(run.End, range.End);
                var p = Math.Max(run.Start, range.Start);
                while (p < end)
                {
                    var level = levels[p - range.Start];
                    var q = p + 1;
                    while (q < end && levels[q - range.Start] == level)
                    {
                        q++;
                    }
                    // The run's own glyphs where the line cuts it safely; reshaped where not.
                    var shaped = level != run.Level
                        ? Shape(run, p, q, level, range.Start, range.End)
                        : p == run.Start && q == run.End
                            ? run.Shaped!
                            : run.Shaped!.Slice(p, q) ?? Shape(run, p, q, level, range.Start, range.End);
                    items.Add(new LineItem(shaped, run.Style, p, q, level, false));
                    p = q;
                }
            }
        }
        if (range.Ellipsis is { } ellipsis)
        {
            items.Add(new LineItem(ellipsis, range.EllipsisStyle!, range.End, range.End, baseLevel, true));
        }

        // Rule L2 on the pieces: each piece's glyphs are already in screen order.
        var itemLevels = new byte[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            itemLevels[i] = items[i].Level;
        }
        var order = BidiParagraph.ReorderLevels(itemLevels);
        var visual = new LineItem[items.Count];
        for (var i = 0; i < order.Length; i++)
        {
            visual[i] = items[order[i]];
        }

        float width = 0f, trailing = 0f, ascent = 0f, descent = 0f;
        var hangFrom = TrimEnd(range.Start, range.End);
        foreach (var item in visual)
        {
            width += item.Shaped.Width;
            if (!item.IsEllipsis && item.End > hangFrom)
            {
                trailing += HangingWidth(item.Shaped, hangFrom);
            }
            var (a, d) = LineMetrics(item.Shaped.Font, item.Style);
            ascent = MathF.Max(ascent, a);
            descent = MathF.Max(descent, d);
        }
        if (visual.Length == 0)
        {
            var style = Text.StyleAt(range.Start);
            (ascent, descent) = LineMetrics(Instance(PreferredFace(style), style), style);
        }

        var direction = (baseLevel & 1) == 0 ? TextDirection.LeftToRight : TextDirection.RightToLeft;
        return new MeasuredLine(visual, direction, LineBreakLengthBefore(range.End, range.Start),
            width, width - trailing, trailing, ascent, descent);
    }

    private static float HangingWidth(ShapedRun shaped, int hangFrom)
    {
        var width = 0f;
        for (var i = 0; i < shaped.Count; i++)
        {
            if (shaped.Clusters[i] >= hangFrom)
            {
                width += shaped.Advances[i];
            }
        }
        return width;
    }

    /// <summary>
    /// How far a font's line box reaches above and below the baseline: its ascent and descent plus
    /// half the leading each (CSS's half-leading), where the leading is what a set line height adds
    /// beyond them, or else the font's own line gap.
    /// </summary>
    private static (float Ascent, float Descent) LineMetrics(FontInstance font, TextStyle style)
    {
        var metrics = font.Metrics(style.Size);
        var content = metrics.Ascender + metrics.Descender;
        var half = ((style.LineHeight ?? content + metrics.LineGap) - content) / 2f;
        return (metrics.Ascender + half, metrics.Descender + half);
    }

    private int RunAt(int index)
    {
        int lo = 0, hi = _runs.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (_runs[mid].End <= index)
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

    private TextLine Place(int index, LineRange range, MeasuredLine line, float width, float top)
    {
        var alignment = Style.Alignment switch
        {
            TextAlignment.Start => line.Direction == TextDirection.LeftToRight ? TextAlignment.Left : TextAlignment.Right,
            TextAlignment.End => line.Direction == TextDirection.LeftToRight ? TextAlignment.Right : TextAlignment.Left,
            _ => Style.Alignment,
        };
        var left = alignment switch
        {
            TextAlignment.Right => width - line.ContentWidth,
            TextAlignment.Center => (width - line.ContentWidth) / 2f,
            _ => 0f,
        };
        // Trailing spaces hang past the end of the line: in right-to-left text that's its left.
        if (line.Direction == TextDirection.RightToLeft)
        {
            left -= line.TrailingWidth;
        }

        var baseline = top + line.Ascent;
        var runs = new GlyphRun[line.Items.Length];
        var boxes = new List<ClusterBox>();
        var x = left;
        for (var i = 0; i < line.Items.Length; i++)
        {
            var item = line.Items[i];
            runs[i] = new GlyphRun(item.Shaped, item.Style, new Vector2(x, baseline), item.Start, item.End, item.IsEllipsis);
            if (item.IsEllipsis)
            {
                boxes.Add(new ClusterBox(item.Start, item.End, x, x + item.Shaped.Width,
                    item.Shaped.Direction == TextDirection.RightToLeft, ClusterBoxKind.Ellipsis));
            }
            else
            {
                AddBoxes(boxes, item, x);
            }
            x += item.Shaped.Width;
        }

        return new TextLine(index, range.Start, range.End, line.LineBreakLength, line.Direction, top,
            line.Ascent, line.Descent, left, line.Width, line.ContentWidth, range.Ellipsis is not null,
            runs, [.. boxes]);
    }

    /// <summary>Adds a box for each grapheme of a piece of a line, left to right.</summary>
    private void AddBoxes(List<ClusterBox> boxes, LineItem item, float x)
    {
        var shaped = item.Shaped;
        var rtl = shaped.Direction == TextDirection.RightToLeft;
        var clusters = shaped.Clusters;
        var advances = shaped.Advances;

        // Clusters are monotonic, so a cluster runs to the next one in text order: the cluster to
        // its right in left-to-right text, to its left in right-to-left text (or the piece's end).
        var leftCluster = item.End;
        var j = 0;
        while (j < shaped.Count)
        {
            var cluster = clusters[j];
            var left = x;
            while (j < shaped.Count && clusters[j] == cluster)
            {
                x += advances[j];
                j++;
            }
            var end = rtl ? leftCluster : j < shaped.Count ? clusters[j] : item.End;
            AddGraphemeBoxes(boxes, cluster, end, left, x, rtl);
            leftCluster = cluster;
        }
    }

    /// <summary>
    /// Adds boxes for the graphemes of one cluster. A cluster of several graphemes (a ligature
    /// such as "ffi") is shared out evenly, so the caret can go between its letters.
    /// </summary>
    private void AddGraphemeBoxes(List<ClusterBox> boxes, int start, int end, float left, float right, bool rtl)
    {
        var kind = IsLineBreak(_text[start]) ? ClusterBoxKind.LineBreak : ClusterBoxKind.Text;
        var first = BoundaryAfter(Graphemes, start);
        var count = 1;
        while (first + count - 1 < Graphemes.Length && Graphemes[first + count - 1] < end)
        {
            count++;
        }
        if (count == 1 || Graphemes[first - 1] != start)
        {
            boxes.Add(new ClusterBox(start, end, left, right, rtl, kind));
            return;
        }

        var share = (right - left) / count;
        for (var v = 0; v < count; v++)
        {
            // Screen order: in right-to-left text the first grapheme is the rightmost.
            var m = rtl ? count - 1 - v : v;
            var gs = m == 0 ? start : Graphemes[first + m - 1];
            var ge = m == count - 1 ? end : Graphemes[first + m];
            boxes.Add(new ClusterBox(gs, ge, left + v * share, left + (v + 1) * share, rtl, kind));
        }
    }
}
