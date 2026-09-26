namespace Radiant.Platform;

/// <summary>
/// Plain text on the system clipboard, for copy and paste. Rich formats (styled text, images,
/// files) come later; text covers text fields.
/// </summary>
public interface IClipboard
{
    /// <summary>Whether the clipboard holds text, so a Paste command can be enabled without reading it.</summary>
    bool HasText { get; }

    /// <summary>The clipboard's text, or null if it holds none.</summary>
    string? GetText();

    /// <summary>Replaces the clipboard's contents with <paramref name="text"/>.</summary>
    void SetText(string text);
}
