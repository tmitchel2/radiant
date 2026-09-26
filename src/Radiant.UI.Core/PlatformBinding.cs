using System;
using System.Drawing;
using Radiant.Platform;

namespace Radiant.UI.Core;

/// <summary>
/// Connects a <see cref="UIRoot"/> to its window's <see cref="IPlatform"/>: the root's cursor
/// is shown through the platform's cursors, its text input client is focused in the platform's
/// text input, and a moving caret moves the input method's candidate window. Owns the platform
/// once attached, and disposes it.
/// </summary>
internal sealed class PlatformBinding(UIRoot root) : IDisposable
{
    private IPlatform? _platform;
    private RectangleF _caret;

    /// <summary>The platform attached, if any.</summary>
    public IPlatform? Platform => _platform;

    /// <summary>Starts forwarding to <paramref name="platform"/>, bringing it up to date with the root.</summary>
    public void Attach(IPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(platform);
        if (_platform is not null)
        {
            throw new InvalidOperationException("A platform is already attached.");
        }
        _platform = platform;
        root.CursorChanged += OnCursorChanged;
        root.TextInputClientChanged += OnClientChanged;
        platform.Cursors.Show(root.Cursor);
        OnClientChanged(root.TextInputClient);
    }

    /// <summary>
    /// After each update: if the client's caret moved (typing, scrolling, layout), tells the
    /// platform, so an open candidate window follows it.
    /// </summary>
    public void AfterUpdate()
    {
        if (_platform is null || root.TextInputClient is not { } client)
        {
            return;
        }
        var caret = client.CaretRect;
        if (caret != _caret)
        {
            _caret = caret;
            _platform.TextInput.InvalidateCaret();
        }
    }

    private void OnCursorChanged(CursorShape shape) => _platform?.Cursors.Show(shape);

    private void OnClientChanged(ITextInputClient? client)
    {
        _caret = client?.CaretRect ?? default;
        _platform?.TextInput.Focus(client);
    }

    /// <summary>Stops forwarding and disposes the platform.</summary>
    public void Dispose()
    {
        if (_platform is not { } platform)
        {
            return;
        }
        root.CursorChanged -= OnCursorChanged;
        root.TextInputClientChanged -= OnClientChanged;
        platform.TextInput.Focus(null);
        _platform = null;
        platform.Dispose();
    }
}
