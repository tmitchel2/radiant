using System;

namespace Radiant.Platform;

/// <summary>A clipboard that is a field: what's set is what's read, within this process.</summary>
public sealed class HeadlessClipboard : IClipboard
{
    private string? _text;

    /// <inheritdoc/>
    public bool HasText => _text is not null;

    /// <inheritdoc/>
    public string? GetText() => _text;

    /// <inheritdoc/>
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
    }

    /// <summary>Empties the clipboard.</summary>
    public void Clear() => _text = null;
}
