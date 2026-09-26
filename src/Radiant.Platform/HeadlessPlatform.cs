namespace Radiant.Platform;

/// <summary>
/// A platform with no operating system behind it: an in-memory clipboard, cursors and
/// appearance that are only recorded, dialogs that answer as scripted, and text input driven by
/// calls. For tests, offscreen rendering, and operating systems without an implementation yet.
/// Its parts are the concrete headless types, so a test can script and inspect them.
/// </summary>
public sealed class HeadlessPlatform : IPlatform
{
    /// <inheritdoc/>
    public string Name => "headless";

    /// <inheritdoc cref="IPlatform.Clipboard"/>
    public HeadlessClipboard Clipboard { get; } = new();

    /// <inheritdoc cref="IPlatform.Cursors"/>
    public HeadlessCursorService Cursors { get; } = new();

    /// <inheritdoc cref="IPlatform.Appearance"/>
    public HeadlessAppearance Appearance { get; } = new();

    /// <inheritdoc cref="IPlatform.Dialogs"/>
    public HeadlessFileDialogs Dialogs { get; } = new();

    /// <inheritdoc cref="IPlatform.TextInput"/>
    public HeadlessTextInput TextInput { get; } = new();

    /// <inheritdoc cref="IPlatform.Chrome"/>
    public HeadlessWindowChrome Chrome { get; } = new();

    IClipboard IPlatform.Clipboard => Clipboard;

    ICursorService IPlatform.Cursors => Cursors;

    IAppearance IPlatform.Appearance => Appearance;

    IFileDialogs IPlatform.Dialogs => Dialogs;

    ITextInput IPlatform.TextInput => TextInput;

    IWindowChrome IPlatform.Chrome => Chrome;

    /// <summary>Nothing to release.</summary>
    public void Dispose()
    {
    }
}
