using System;

namespace Radiant.Platform.MacOS;

/// <summary>
/// The platform on macOS, through AppKit: <c>NSPasteboard</c>, <c>NSCursor</c>, the
/// appearance and accessibility settings, <c>NSOpenPanel</c>/<c>NSSavePanel</c>, and
/// <c>NSTextInputClient</c> on the window's GLFW content view. Create it on the main thread,
/// once the window is open:
/// <code>
/// RadiantUI.Run(app, new UIAppOptions { Platform = MacPlatform.CreateOrHeadless });
/// </code>
/// </summary>
public sealed class MacPlatform : IPlatform
{
    private readonly MacClipboard _clipboard;
    private readonly MacCursorService _cursors;
    private readonly MacAppearance _appearance;
    private readonly MacFileDialogs _dialogs;
    private readonly ITextInput _textInput;
    private readonly MacWindowChrome _chrome;

    private MacPlatform(nint window)
    {
        EnsureApplication();
        _clipboard = MacClipboard.General();
        _cursors = new MacCursorService();
        _appearance = new MacAppearance();
        _dialogs = new MacFileDialogs(window);
        _chrome = new MacWindowChrome(window);
        Window = window;
        using (ObjC.Pool())
        {
            View = window == 0 ? 0 : ObjC.Send(window, "contentView");
        }
        // Without a window there's nothing to type into; a headless input keeps the API whole.
        _textInput = View == 0 ? new HeadlessTextInput() : new MacTextInput(View, _cursors);
    }

    /// <summary>The <c>NSWindow*</c> served, or zero.</summary>
    internal nint Window { get; }

    /// <summary>The window's content view (GLFW's), which takes the text input; zero without a window.</summary>
    internal nint View { get; }

    /// <summary>Whether this process is on macOS, where <see cref="Create"/> works.</summary>
    public static bool IsSupported => OperatingSystem.IsMacOS();

    /// <summary>
    /// The platform for <paramref name="window"/>, or with no window (<c>default</c>) the
    /// services that don't need one: clipboard, cursors, appearance and app-modal dialogs.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Not on macOS.</exception>
    public static MacPlatform Create(NativeWindow window = default) =>
        IsSupported ? new MacPlatform(window.Cocoa) : throw new PlatformNotSupportedException("MacPlatform needs macOS.");

    /// <summary>
    /// The macOS platform on macOS, and a <see cref="HeadlessPlatform"/> elsewhere: for apps that
    /// build for several systems before each has its own platform.
    /// </summary>
    public static IPlatform CreateOrHeadless(NativeWindow window) => IsSupported ? Create(window) : new HeadlessPlatform();

    /// <summary>
    /// Makes sure the process has its <c>NSApplication</c>. GLFW makes it when it starts; a tool
    /// or test using the platform without a window needs it too, as AppKit's system cursors
    /// are nil until it exists.
    /// </summary>
    internal static void EnsureApplication()
    {
        if (ObjC.AppKitConstant("NSApp") == 0)
        {
            using var pool = ObjC.Pool();
            ObjC.Send(ObjC.Class("NSApplication"), "sharedApplication");
        }
    }

    /// <inheritdoc/>
    public string Name => "macOS";

    /// <inheritdoc/>
    public IWindowChrome Chrome => _chrome;

    /// <inheritdoc/>
    public IClipboard Clipboard => _clipboard;

    /// <inheritdoc/>
    public ICursorService Cursors => _cursors;

    /// <inheritdoc/>
    public IAppearance Appearance => _appearance;

    /// <inheritdoc/>
    public IFileDialogs Dialogs => _dialogs;

    /// <inheritdoc/>
    public ITextInput TextInput => _textInput;

    /// <summary>Stops observing appearance and hands text input back to GLFW.</summary>
    public void Dispose()
    {
        (_textInput as IDisposable)?.Dispose();
        _appearance.Dispose();
        _clipboard.Dispose();
    }
}
