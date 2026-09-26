namespace Radiant.Text;

/// <summary>Ways a caret moves through a paragraph, as the keyboard moves it.</summary>
public enum CaretMovement
{
    /// <summary>One character left on screen (arrow key): backwards in left-to-right text, forwards in right-to-left.</summary>
    Left,

    /// <summary>One character right on screen.</summary>
    Right,

    /// <summary>One character forwards in reading order.</summary>
    NextCharacter,

    /// <summary>One character backwards in reading order.</summary>
    PreviousCharacter,

    /// <summary>To the end of the word after the caret (the next one if it's between words).</summary>
    NextWord,

    /// <summary>To the start of the word before the caret.</summary>
    PreviousWord,

    /// <summary>A word to the left on screen (Option+Left): the previous word in left-to-right text, the next in right-to-left.</summary>
    WordLeft,

    /// <summary>A word to the right on screen.</summary>
    WordRight,

    /// <summary>To the line above, keeping the caret's horizontal position.</summary>
    Up,

    /// <summary>To the line below, keeping the caret's horizontal position.</summary>
    Down,

    /// <summary>To the start of the line in reading order (Home, Cmd+Left in left-to-right text).</summary>
    LineStart,

    /// <summary>To the end of the line in reading order, before any line break.</summary>
    LineEnd,

    /// <summary>To the start of the text.</summary>
    DocumentStart,

    /// <summary>To the end of the text.</summary>
    DocumentEnd,
}
