using System;
using Radiant.Text;

namespace Radiant.UI.Core;

/// <summary>
/// Everything about editable text that changes as the user edits: the text, the selection, and the
/// range an input method is still composing (shown underlined, replaced as the user picks).
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="Selection">The selection or caret.</param>
/// <param name="Composing">The input method's unfinished text, or null.</param>
public sealed record TextEditState(string Text, TextSelection Selection, TextRange? Composing = null)
{
    /// <summary>Empty text with the caret at the start.</summary>
    public static TextEditState Empty { get; } = new("", TextSelection.Caret(0));

    /// <summary>Text with the caret at its end.</summary>
    public static TextEditState From(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new TextEditState(text, TextSelection.Caret(text.Length));
    }

    /// <summary>The selected text.</summary>
    public string SelectedText => Text[Selection.Start..Selection.End];
}
