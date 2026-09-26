using System;
using System.Globalization;
using Radiant.Text;
using Radiant.Text.Unicode;

namespace Radiant.UI.Core;

/// <summary>
/// The edits text fields make, as pure functions from one <see cref="TextEditState"/> to the next.
/// Caret movement uses the laid-out <see cref="Paragraph"/> (so arrows follow bidi text on screen,
/// and up and down follow lines); the rest works on the text alone, by grapheme and word.
/// </summary>
public static class TextEditing
{
    /// <summary>Replaces the selection (or composing text) with <paramref name="text"/>, leaving the caret after it.</summary>
    public static TextEditState Insert(TextEditState state, string text)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(text);
        var range = state.Composing ?? state.Selection.Range;
        var result = state.Text[..range.Start] + text + state.Text[range.End..];
        return new TextEditState(result, TextSelection.Caret(range.Start + text.Length));
    }

    /// <summary>
    /// Deletes the selection, or else the grapheme (or, with <paramref name="byWord"/>, the word)
    /// before the caret: Backspace, and Option+Backspace.
    /// </summary>
    public static TextEditState DeleteBackward(TextEditState state, bool byWord = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.Selection.IsCollapsed)
        {
            return Insert(state, "");
        }
        var caret = state.Selection.Focus;
        if (caret == 0)
        {
            return state;
        }
        var start = byWord ? PreviousWordStart(state.Text, caret) : GraphemeBoundaries.Previous(state.Text, caret);
        return Remove(state, start, caret);
    }

    /// <summary>Deletes the selection, or else the grapheme (or word) after the caret: Delete.</summary>
    public static TextEditState DeleteForward(TextEditState state, bool byWord = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.Selection.IsCollapsed)
        {
            return Insert(state, "");
        }
        var caret = state.Selection.Focus;
        if (caret == state.Text.Length)
        {
            return state;
        }
        var end = byWord ? NextWordEnd(state.Text, caret) : GraphemeBoundaries.Next(state.Text, caret);
        return Remove(state, caret, end);
    }

    /// <summary>Deletes from the caret back to the start of its line (Command+Backspace).</summary>
    public static TextEditState DeleteToLineStart(TextEditState state, Paragraph paragraph)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(paragraph);
        if (!state.Selection.IsCollapsed)
        {
            return Insert(state, "");
        }
        var start = paragraph.MoveCaret(state.Selection.FocusPosition, CaretMovement.LineStart).Index;
        return Remove(state, start, state.Selection.Focus);
    }

    /// <summary>
    /// Moves the caret, or with <paramref name="extend"/> (Shift held) the selection's moving end.
    /// Without extending, Left and Right on a selection collapse it to that side.
    /// </summary>
    public static TextEditState Move(TextEditState state, Paragraph paragraph, CaretMovement movement, bool extend, float? goalX = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(paragraph);
        var selection = state.Selection;
        if (!extend && !selection.IsCollapsed && movement is CaretMovement.Left or CaretMovement.Right)
        {
            // Collapse to the side on screen the arrow points to.
            var start = paragraph.GetCaretRect(new TextPosition(selection.Start)).Left;
            var end = paragraph.GetCaretRect(new TextPosition(selection.End)).Left;
            var leftIndex = start <= end ? selection.Start : selection.End;
            var rightIndex = start <= end ? selection.End : selection.Start;
            return state with { Selection = TextSelection.Caret(movement == CaretMovement.Left ? leftIndex : rightIndex), Composing = null };
        }
        var moved = paragraph.MoveCaret(selection.FocusPosition, movement, goalX);
        var next = extend ? new TextSelection(selection.Anchor, moved.Index, moved.Affinity) : TextSelection.Caret(moved.Index, moved.Affinity);
        return state with { Selection = next, Composing = null };
    }

    /// <summary>Selects everything.</summary>
    public static TextEditState SelectAll(TextEditState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state with { Selection = new TextSelection(0, state.Text.Length), Composing = null };
    }

    /// <summary>Selects the word at an index (a double click).</summary>
    public static TextEditState SelectWord(TextEditState state, Paragraph paragraph, int index)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(paragraph);
        var word = paragraph.GetWordRange(index);
        return state with { Selection = new TextSelection(word.Start, word.End), Composing = null };
    }

    /// <summary>Selects the line an index is on, without its line break (a triple click).</summary>
    public static TextEditState SelectLine(TextEditState state, Paragraph paragraph, TextPosition position)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(paragraph);
        var line = paragraph.Lines[paragraph.GetLineIndex(position)];
        return state with { Selection = new TextSelection(line.Start, line.End - line.LineBreakLength), Composing = null };
    }

    /// <summary>
    /// Shows an input method's unfinished text in place of the selection (or the previous
    /// unfinished text), with the input method's selection inside it.
    /// </summary>
    public static TextEditState SetComposing(TextEditState state, string text, int selectionStart, int selectionLength)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return EndComposing(Insert(state, ""));
        }
        var range = state.Composing ?? state.Selection.Range;
        var result = state.Text[..range.Start] + text + state.Text[range.End..];
        var start = range.Start + Math.Clamp(selectionStart, 0, text.Length);
        var end = Math.Min(start + Math.Max(0, selectionLength), range.Start + text.Length);
        return new TextEditState(result, new TextSelection(start, end), new TextRange(range.Start, range.Start + text.Length));
    }

    /// <summary>Accepts the unfinished text as it is.</summary>
    public static TextEditState EndComposing(TextEditState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Composing is null ? state : state with { Composing = null };
    }

    private static TextEditState Remove(TextEditState state, int start, int end) =>
        new(state.Text[..start] + state.Text[end..], TextSelection.Caret(start));

    private static int PreviousWordStart(string text, int index)
    {
        var words = WordBoundaries.Get(text);
        for (var i = words.Count - 1; i >= 0; i--)
        {
            if (words[i] < index && WordBoundaries.IsWord(text, words[i], Math.Min(index, i + 1 < words.Count ? words[i + 1] : text.Length)))
            {
                return words[i];
            }
        }
        return 0;
    }

    private static int NextWordEnd(string text, int index)
    {
        var words = WordBoundaries.Get(text);
        for (var i = 1; i < words.Count; i++)
        {
            if (words[i] > index && WordBoundaries.IsWord(text, Math.Max(index, words[i - 1]), words[i]))
            {
                return words[i];
            }
        }
        return text.Length;
    }

    internal static string Describe(TextEditState state) => string.Create(CultureInfo.InvariantCulture,
        $"{state.Text[..state.Selection.Start]}[{state.SelectedText}]{state.Text[state.Selection.End..]}");
}
