using System.Drawing;

namespace Radiant.Platform;

/// <summary>
/// Something in the UI that takes typed text, such as a text field, as the platform's text input
/// sees it. The UI implements this and hands it to <see cref="ITextInput.Focus"/> while it has
/// focus.
/// <para>
/// Input methods (Japanese, Chinese, Korean, dead keys for accents) <em>compose</em> text before
/// committing it: the user types "nihongo", sees the provisional text marked (usually
/// underlined) where the caret is, picks a conversion from a candidate window, and only then is
/// the text committed. The client keeps a <em>composition</em>, a range of its text that is
/// marked, and three calls move it along:
/// </para>
/// <list type="bullet">
/// <item><see cref="SetMarkedText"/> replaces the composition (or, if there is none, inserts at
/// the caret) with provisional text.</item>
/// <item><see cref="InsertText"/> replaces the composition (or the selection) with final text,
/// and ends the composition.</item>
/// <item><see cref="UnmarkText"/> ends the composition, keeping its text as ordinary text.</item>
/// </list>
/// All indices are in UTF-16 code units, as .NET strings count.
/// </summary>
public interface ITextInputClient
{
    /// <summary>
    /// Commits <paramref name="text"/>: it replaces the composition if there is one, otherwise the
    /// selection, and the composition ends. Plain typing arrives here, one character at a time.
    /// </summary>
    void InsertText(string text);

    /// <summary>
    /// Shows <paramref name="text"/> as the composition, replacing the previous one (or the
    /// selection, when a composition starts). An empty <paramref name="text"/> removes the
    /// composition: the user cancelled it.
    /// </summary>
    /// <param name="text">The provisional text.</param>
    /// <param name="selectionStart">Where the caret or selection is within <paramref name="text"/>.</param>
    /// <param name="selectionLength">The selection's length within it; 0 for a caret. Input methods
    /// select the clause being converted.</param>
    void SetMarkedText(string text, int selectionStart, int selectionLength);

    /// <summary>Ends the composition, keeping its text as if it had been inserted.</summary>
    void UnmarkText();

    /// <summary>
    /// Where the caret is, in the window's content coordinates (logical pixels, top-left origin),
    /// as tall as the line. The platform places the candidate window just below it. While
    /// composing, the start of the composition is a good choice, so the window doesn't move as
    /// the user types.
    /// </summary>
    RectangleF CaretRect { get; }
}
