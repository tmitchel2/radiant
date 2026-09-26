namespace Radiant.UI.Core;

/// <summary>Text typed, sent to the focused box and its ancestors.</summary>
public sealed class TextInputEventArgs : UIEventArgs
{
    internal TextInputEventArgs(string text) => Text = text;

    /// <summary>The text: usually one character, or several from an input method.</summary>
    public string Text { get; }
}
