namespace Radiant.UI.Core;

/// <summary>A clipboard private to the process: the fallback where no platform clipboard is provided, and for tests.</summary>
public sealed class MemoryClipboard : ITextClipboard
{
    private string? _text;

    /// <inheritdoc/>
    public string? GetText() => _text;

    /// <inheritdoc/>
    public void SetText(string text) => _text = text;
}
