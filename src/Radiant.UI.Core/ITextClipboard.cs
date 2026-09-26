namespace Radiant.UI.Core;

/// <summary>Where text inputs copy to and paste from: the system clipboard, supplied by the platform.</summary>
public interface ITextClipboard
{
    /// <summary>The clipboard's text, or null if it holds none.</summary>
    string? GetText();

    /// <summary>Puts text on the clipboard.</summary>
    void SetText(string text);
}
