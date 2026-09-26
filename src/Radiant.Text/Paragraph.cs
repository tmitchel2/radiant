using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Text.Unicode;

namespace Radiant.Text;

/// <summary>
/// Styled text laid out in lines: shaped, broken at a width, reordered for bidi, aligned. Answers
/// the questions a text view and editor ask of it: where each glyph goes, where a click lands,
/// where the caret is drawn, what a selection covers, and where the caret moves on a key press.
/// <para>
/// Coordinates are pixels with y down, from the paragraph's top left. Text indices are UTF-16.
/// A paragraph is immutable; <see cref="WithMaxWidth"/> lays the same text out at another width
/// without shaping it again.
/// </para>
/// </summary>
public sealed class Paragraph
{
    private readonly ParagraphBuilder _builder;
    private readonly TextLine[] _lines;

    internal Paragraph(ParagraphBuilder builder, ParagraphStyle style, TextLine[] lines, float width,
        float height, float longestLine, bool didExceedMaxLines)
    {
        _builder = builder;
        Style = style;
        _lines = lines;
        Width = width;
        Height = height;
        LongestLine = longestLine;
        DidExceedMaxLines = didExceedMaxLines;
    }

    /// <summary>Lays out styled text.</summary>
    /// <param name="text">The text and its styles.</param>
    /// <param name="style">Width, alignment, direction and line limit; <see cref="ParagraphStyle.Default"/> if null.</param>
    /// <param name="fonts">Where font families are found; <see cref="FontLibrary.Default"/> if null.</param>
    public static Paragraph Layout(AttributedText text, ParagraphStyle? style = null, FontLibrary? fonts = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        style ??= ParagraphStyle.Default;
        return new ParagraphBuilder(text, style, fonts ?? FontLibrary.Default).Layout(style.MaxWidth);
    }

    /// <summary>Lays out text in one style.</summary>
    public static Paragraph Layout(string text, TextStyle? textStyle = null, ParagraphStyle? style = null, FontLibrary? fonts = null) =>
        Layout(AttributedText.Plain(text, textStyle), style, fonts);

    /// <summary>The same text laid out at another width. Shaping is reused.</summary>
    public Paragraph WithMaxWidth(float maxWidth) => _builder.Layout(maxWidth);

    /// <summary>The text laid out.</summary>
    public AttributedText Text => _builder.Text;

    /// <summary>The style it was laid out with.</summary>
    public ParagraphStyle Style { get; }

    /// <summary>The lines, top to bottom. There is always at least one, even for empty text.</summary>
    public IReadOnlyList<TextLine> Lines => _lines;

    /// <summary>The width lines are aligned in: <see cref="ParagraphStyle.MaxWidth"/>, or the longest line's when that's infinite.</summary>
    public float Width { get; }

    /// <summary>The height of all the lines.</summary>
    public float Height { get; }

    /// <summary>The widest line's width, without trailing spaces.</summary>
    public float LongestLine { get; }

    /// <summary>The narrowest the paragraph can be without breaking inside a word: its widest word.</summary>
    public float MinIntrinsicWidth => _builder.MinIntrinsicWidth;

    /// <summary>The width the paragraph takes without wrapping: its widest line between line breaks.</summary>
    public float MaxIntrinsicWidth => _builder.MaxIntrinsicWidth;

    /// <summary>Whether lines were cut to fit <see cref="ParagraphStyle.MaxLines"/>.</summary>
    public bool DidExceedMaxLines { get; }

    /// <summary>The first line's baseline, for aligning text with its neighbours.</summary>
    public float FirstBaseline => _lines[0].Baseline;

    /// <summary>The last line's baseline.</summary>
    public float LastBaseline => _lines[^1].Baseline;

    private string Source => _builder.Text.Text;

    /// <summary>The line a caret position is on.</summary>
    public int GetLineIndex(TextPosition position)
    {
        var index = Math.Clamp(position.Index, 0, Source.Length);
        int lo = 0, hi = _lines.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) >>> 1;
            if (_lines[mid].Start <= index)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }
        // At a wrap, the index ends one line and starts the next: upstream means the first.
        if (lo > 0 && index == _lines[lo].Start && position.Affinity == TextAffinity.Upstream
            && _lines[lo - 1].LineBreakLength == 0)
        {
            lo--;
        }
        return lo;
    }

    /// <summary>The caret at a position: a zero-width box the height of its line.</summary>
    public TextBox GetCaretRect(TextPosition position)
    {
        var line = _lines[GetLineIndex(position)];
        var (x, direction) = CaretX(line, Math.Clamp(position.Index, 0, Source.Length), position.Affinity);
        return new TextBox(x, line.Top, x, line.Bottom, direction);
    }

    /// <summary>The caret position nearest a point, as a click there would place it.</summary>
    public TextPosition HitTest(Vector2 point)
    {
        var line = _lines[^1];
        foreach (var candidate in _lines)
        {
            if (point.Y < candidate.Bottom)
            {
                line = candidate;
                break;
            }
        }
        return HitTest(line, point.X);
    }

    /// <summary>
    /// The boxes covering text[start..end), one per line and direction run, merged where they
    /// touch, for drawing a selection. The order of the ends doesn't matter.
    /// </summary>
    public IReadOnlyList<TextBox> GetSelectionRects(int start, int end)
    {
        if (start > end)
        {
            (start, end) = (end, start);
        }
        var result = new List<TextBox>();
        foreach (var line in _lines)
        {
            if (line.End <= start || line.Start >= end)
            {
                continue;
            }
            TextBox? open = null;
            foreach (var box in line.Boxes)
            {
                if (box.Kind == ClusterBoxKind.Ellipsis)
                {
                    continue;
                }
                var from = Math.Max(box.Start, start);
                var to = Math.Min(box.End, end);
                if (from >= to)
                {
                    continue;
                }
                var (left, right) = Portion(box, from, to);
                if (open is { } current && MathF.Abs(left - current.Right) < 0.5f)
                {
                    open = current with { Right = right };
                }
                else
                {
                    AddIfVisible(result, open);
                    var direction = box.IsRightToLeft ? TextDirection.RightToLeft : TextDirection.LeftToRight;
                    open = new TextBox(left, line.Top, right, line.Bottom, direction);
                }
            }
            AddIfVisible(result, open);
        }
        return result;

        static void AddIfVisible(List<TextBox> boxes, TextBox? box)
        {
            if (box is { Width: > 0f } visible)
            {
                boxes.Add(visible);
            }
        }
    }

    /// <summary>The word (or run of spaces or punctuation) at an index, as a double click selects it.</summary>
    public TextRange GetWordRange(int index)
    {
        var words = _builder.Words;
        index = Math.Clamp(index, 0, Source.Length);
        var after = ParagraphBuilder.BoundaryAfter(words, index);
        if (words[after] <= index)
        {
            // At the end of the text: the last segment.
            return words.Length > 1 ? new TextRange(words[^2], words[^1]) : new TextRange(index, index);
        }
        return new TextRange(words[after - 1], words[after]);
    }

    /// <summary>Where a caret moves from <paramref name="position"/>.</summary>
    /// <param name="position">Where the caret is.</param>
    /// <param name="movement">How it moves.</param>
    /// <param name="goalX">
    /// For <see cref="CaretMovement.Up"/> and <see cref="CaretMovement.Down"/>, the x to aim for:
    /// keep the x from before the first of a series of vertical moves, so the caret returns to its
    /// column after passing a short line. Null uses the caret's current x.
    /// </param>
    public TextPosition MoveCaret(TextPosition position, CaretMovement movement, float? goalX = null)
    {
        var index = Math.Clamp(position.Index, 0, Source.Length);
        position = position with { Index = index };
        var line = _lines[GetLineIndex(position)];
        var leftToRight = line.Direction == TextDirection.LeftToRight;
        return movement switch
        {
            CaretMovement.Left => MoveVisually(position, line, right: false),
            CaretMovement.Right => MoveVisually(position, line, right: true),
            CaretMovement.NextCharacter => new TextPosition(Next(_builder.Graphemes, index)),
            CaretMovement.PreviousCharacter => new TextPosition(Previous(_builder.Graphemes, index)),
            CaretMovement.NextWord => new TextPosition(NextWordEnd(index)),
            CaretMovement.PreviousWord => new TextPosition(PreviousWordStart(index)),
            CaretMovement.WordLeft => new TextPosition(leftToRight ? PreviousWordStart(index) : NextWordEnd(index)),
            CaretMovement.WordRight => new TextPosition(leftToRight ? NextWordEnd(index) : PreviousWordStart(index)),
            CaretMovement.Up => MoveVertically(position, line, -1, goalX),
            CaretMovement.Down => MoveVertically(position, line, 1, goalX),
            CaretMovement.LineStart => new TextPosition(line.Start),
            CaretMovement.LineEnd => LineEnd(line),
            CaretMovement.DocumentStart => new TextPosition(0),
            CaretMovement.DocumentEnd => new TextPosition(Source.Length),
            _ => throw new ArgumentOutOfRangeException(nameof(movement), movement, null),
        };
    }

    /// <summary>The end of a line's text: before its line break, or upstream of a wrap.</summary>
    private TextPosition LineEnd(TextLine line) =>
        line.LineBreakLength == 0 && line.Index < _lines.Length - 1
            ? new TextPosition(line.End, TextAffinity.Upstream)
            : new TextPosition(line.ContentEnd);

    private static (float X, TextDirection Direction) CaretX(TextLine line, int index, TextAffinity affinity)
    {
        ClusterBox? before = null;
        foreach (var box in line.Boxes)
        {
            if (box.Kind == ClusterBoxKind.Ellipsis)
            {
                continue;
            }
            if (box.Start <= index && index < box.End
                && (affinity == TextAffinity.Downstream || index == line.Start))
            {
                return (box.LeadingEdge, DirectionOf(box));
            }
            if (box.Start < index && index <= box.End)
            {
                before = box;
            }
        }
        if (before is { } previous)
        {
            return (previous.TrailingEdge, DirectionOf(previous));
        }
        // No text on the line (an empty line): where text would start.
        return line.Direction == TextDirection.LeftToRight
            ? (line.Left, line.Direction)
            : (line.Left + line.Width, line.Direction);
    }

    private static TextDirection DirectionOf(ClusterBox box) =>
        box.IsRightToLeft ? TextDirection.RightToLeft : TextDirection.LeftToRight;

    private static TextPosition HitTest(TextLine line, float x)
    {
        ClusterBox? first = null, last = null;
        foreach (var box in line.Boxes)
        {
            if (box.Kind != ClusterBoxKind.Text)
            {
                continue;
            }
            first ??= box;
            last = box;
            if (x < box.Right && x >= box.Left)
            {
                return x < (box.Left + box.Right) / 2f ? LeftEdge(box) : RightEdge(box);
            }
        }
        if (first is null)
        {
            return new TextPosition(line.Start);
        }
        return x < first.Value.Left ? LeftEdge(first.Value) : RightEdge(last!.Value);

        static TextPosition LeftEdge(ClusterBox box) => box.IsRightToLeft
            ? new TextPosition(box.End, TextAffinity.Upstream)
            : new TextPosition(box.Start);

        static TextPosition RightEdge(ClusterBox box) => box.IsRightToLeft
            ? new TextPosition(box.Start)
            : new TextPosition(box.End, TextAffinity.Upstream);
    }

    /// <summary>The part of a box covering text[from..to), for a selection that ends inside a grapheme.</summary>
    private static (float Left, float Right) Portion(ClusterBox box, int from, int to)
    {
        var length = box.End - box.Start;
        var width = box.Right - box.Left;
        var a = (from - box.Start) / (float)length * width;
        var b = (to - box.Start) / (float)length * width;
        return box.IsRightToLeft ? (box.Right - b, box.Right - a) : (box.Left + a, box.Left + b);
    }

    /// <summary>
    /// Moves to the nearest caret stop left or right on screen. Past the end of the line it goes
    /// to the neighbouring line in reading order, to the stop on the side the caret came from.
    /// </summary>
    private TextPosition MoveVisually(TextPosition position, TextLine line, bool right)
    {
        var x = CaretX(line, position.Index, position.Affinity).X;
        (TextPosition Position, float X)? best = null;
        foreach (var stop in CaretStops(line))
        {
            var beyond = right ? stop.X > x + 0.01f : stop.X < x - 0.01f;
            var closer = best is null || (right ? stop.X < best.Value.X : stop.X > best.Value.X);
            if (beyond && closer)
            {
                best = stop;
            }
        }
        if (best is { } found)
        {
            return found.Position;
        }

        var forward = right == (line.Direction == TextDirection.LeftToRight);
        var next = line.Index + (forward ? 1 : -1);
        if (next < 0 || next >= _lines.Length)
        {
            return position;
        }
        var stops = CaretStops(_lines[next]);
        stops.Sort((a, b) => right ? a.X.CompareTo(b.X) : b.X.CompareTo(a.X));
        // Arriving at the same index as the wrap just left would look like no move: take one more.
        var target = stops[0].Position.Index == position.Index && stops.Count > 1 ? stops[1] : stops[0];
        return target.Position;
    }

    private List<(TextPosition Position, float X)> CaretStops(TextLine line)
    {
        var stops = new List<(TextPosition, float)>();
        var graphemes = _builder.Graphemes;
        var end = line.ContentEnd;
        for (var g = ParagraphBuilder.BoundaryAfter(graphemes, line.Start - 1); g < graphemes.Length && graphemes[g] <= end; g++)
        {
            var index = graphemes[g];
            var affinity = index == line.End && index > line.Start ? TextAffinity.Upstream : TextAffinity.Downstream;
            stops.Add((new TextPosition(index, affinity), CaretX(line, index, affinity).X));
        }
        return stops;
    }

    private TextPosition MoveVertically(TextPosition position, TextLine line, int delta, float? goalX)
    {
        var target = line.Index + delta;
        if (target < 0)
        {
            return new TextPosition(0);
        }
        if (target >= _lines.Length)
        {
            return new TextPosition(Source.Length);
        }
        var x = goalX ?? CaretX(line, position.Index, position.Affinity).X;
        return HitTest(_lines[target], x);
    }

    private int NextWordEnd(int index)
    {
        var words = _builder.Words;
        for (var i = ParagraphBuilder.BoundaryAfter(words, index); i < words.Length && words[i] > index; i++)
        {
            if (WordBoundaries.IsWord(Source, Math.Max(index, words[i - 1]), words[i]))
            {
                return words[i];
            }
        }
        return Source.Length;
    }

    private int PreviousWordStart(int index)
    {
        var words = _builder.Words;
        for (var i = ParagraphBuilder.BoundaryAfter(words, index - 1) - 1; i >= 0; i--)
        {
            if (words[i] < index && WordBoundaries.IsWord(Source, words[i], Math.Min(index, words[i + 1])))
            {
                return words[i];
            }
        }
        return 0;
    }

    private static int Next(int[] boundaries, int index) =>
        boundaries[ParagraphBuilder.BoundaryAfter(boundaries, index)] is var next && next > index ? next : index;

    private static int Previous(int[] boundaries, int index)
    {
        var i = ParagraphBuilder.BoundaryAfter(boundaries, index - 1) - 1;
        return i >= 0 ? boundaries[i] : 0;
    }
}
