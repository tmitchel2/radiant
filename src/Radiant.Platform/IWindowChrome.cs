namespace Radiant.Platform;

/// <summary>
/// The window's frame: whether the app draws its own title bar, where the system's window
/// controls sit in it, and moving and zooming the window from it.
/// </summary>
public interface IWindowChrome
{
    /// <summary>Whether the app can draw its own title bar on this platform and window.</summary>
    bool IsSupported { get; }

    /// <summary>Whether the app is drawing its own title bar, the window's content reaching its top edge.</summary>
    bool ExtendsIntoTitleBar { get; }

    /// <summary>
    /// Has the window's content reach its top edge under a transparent title bar, the system's
    /// window controls (macOS's traffic lights) staying over it; or goes back to the system's bar.
    /// </summary>
    void ExtendIntoTitleBar(bool extend);

    /// <summary>The system title bar's height, which a drawn title bar should match or exceed.</summary>
    float TitleBarHeight { get; }

    /// <summary>How far in from the left the system's window controls reach, which a drawn title bar leaves clear.</summary>
    float LeadingInset { get; }

    /// <summary>How far in from the right the system's window controls reach (Windows' caption buttons).</summary>
    float TrailingInset { get; }

    /// <summary>
    /// Moves the window with the pointer, from a press on the drawn title bar: call it while
    /// handling the press. Returns when the pointer is released.
    /// </summary>
    void BeginDrag();

    /// <summary>What a double click on the title bar does: zooms (or minimises, as the user has chosen) the window.</summary>
    void TitleBarDoubleClick();
}
