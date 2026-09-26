using System;

namespace Radiant.Platform;

/// <summary>
/// The operating system's services for one window: the clipboard, the pointer cursor, the user's
/// appearance settings, file dialogs and text input. The UI talks to these interfaces, never to
/// an operating system directly, so it runs the same on every platform and headless in tests.
/// <para>
/// A platform belongs to the UI thread: call it from there, and its events arrive there.
/// Disposing it undoes whatever it installed (observers, text input hooks).
/// </para>
/// </summary>
public interface IPlatform : IDisposable
{
    /// <summary>A short name for diagnostics, such as <c>macOS</c> or <c>headless</c>.</summary>
    string Name { get; }

    /// <summary>Text on the system clipboard.</summary>
    IClipboard Clipboard { get; }

    /// <summary>The pointer's shape.</summary>
    ICursorService Cursors { get; }

    /// <summary>Dark mode, the accent colour and accessibility display settings.</summary>
    IAppearance Appearance { get; }

    /// <summary>Open and save panels.</summary>
    IFileDialogs Dialogs { get; }

    /// <summary>Text input, including input methods that compose text before committing it.</summary>
    ITextInput TextInput { get; }

    /// <summary>The window's frame: drawing the app's own title bar, and moving the window from it.</summary>
    IWindowChrome Chrome { get; }

    /// <summary>The platform's own menus.</summary>
    IMenuService Menus { get; }

    /// <summary>Assistive technology reading and acting on the UI.</summary>
    IAccessibility Accessibility { get; }
}
