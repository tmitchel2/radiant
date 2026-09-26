namespace Radiant.Platform.MacOS;

/// <summary>
/// The window's frame through <c>NSWindow</c>: a full-size content view under a transparent title
/// bar with its title hidden, the traffic lights left where they are; window drags with
/// <c>performWindowDragWithEvent:</c>; and the user's double-click choice (zoom, minimise, nothing).
/// </summary>
internal sealed class MacWindowChrome(nint window) : IWindowChrome
{
    private const nint FullSizeContentView = 1 << 15;
    private const nint TitleHidden = 1;
    private const nint CloseButton = 0;
    private const nint ZoomButton = 2;
    private const nint LeftMouseDown = 1;

    /// <inheritdoc/>
    public bool IsSupported => window != 0;

    /// <inheritdoc/>
    public bool ExtendsIntoTitleBar
    {
        get
        {
            if (window == 0)
            {
                return false;
            }
            using var pool = ObjC.Pool();
            return (ObjC.Send(window, "styleMask") & FullSizeContentView) != 0;
        }
    }

    /// <inheritdoc/>
    public void ExtendIntoTitleBar(bool extend)
    {
        if (window == 0 || extend == ExtendsIntoTitleBar)
        {
            return;
        }
        using var pool = ObjC.Pool();
        // Changing the style keeps the content's size and moves the frame round it; the window
        // should keep its place and size instead, the content growing into the title bar (or
        // giving it back). Putting the frame back resizes the content, which GLFW, and so the
        // renderer, hear about as a resize.
        var frame = ObjC.GetRect(window, "frame");
        var mask = ObjC.Send(window, "styleMask");
        ObjC.Send(window, "setStyleMask:", extend ? mask | FullSizeContentView : mask & ~FullSizeContentView);
        ObjC.SendBool(window, "setTitlebarAppearsTransparent:", extend);
        ObjC.Send(window, "setTitleVisibility:", extend ? TitleHidden : 0);
        ObjC.SendRectBool(window, "setFrame:display:", frame, true);
    }

    /// <inheritdoc/>
    public float TitleBarHeight
    {
        get
        {
            if (window == 0)
            {
                return 0f;
            }
            using var pool = ObjC.Pool();
            var frame = ObjC.GetRect(window, "frame");
            var layout = ObjC.GetRect(window, "contentLayoutRect");
            return (float)(frame.Height - layout.Height);
        }
    }

    /// <inheritdoc/>
    public float LeadingInset
    {
        get
        {
            if (window == 0)
            {
                return 0f;
            }
            using var pool = ObjC.Pool();
            // Past the last traffic light (the zoom button), with the space the first (close) has
            // before it again after it.
            var close = ObjC.Send(window, "standardWindowButton:", CloseButton);
            var zoom = ObjC.Send(window, "standardWindowButton:", ZoomButton);
            if (close == 0 || zoom == 0)
            {
                return 0f;
            }
            var first = ObjC.GetRect(close, "convertRect:toView:", ObjC.GetRect(close, "bounds"), 0);
            var last = ObjC.GetRect(zoom, "convertRect:toView:", ObjC.GetRect(zoom, "bounds"), 0);
            return (float)(last.X + last.Width + first.X);
        }
    }

    /// <inheritdoc/>
    public float TrailingInset => 0f;

    /// <inheritdoc/>
    public void BeginDrag()
    {
        if (window == 0)
        {
            return;
        }
        using var pool = ObjC.Pool();
        // The press being handled is AppKit's current event: GLFW passes it on from mouseDown:.
        var app = ObjC.AppKitConstant("NSApp");
        var current = app == 0 ? 0 : ObjC.Send(app, "currentEvent");
        if (current != 0 && ObjC.Send(current, "type") == LeftMouseDown)
        {
            ObjC.Send(window, "performWindowDragWithEvent:", current);
        }
    }

    /// <inheritdoc/>
    public void TitleBarDoubleClick()
    {
        if (window == 0)
        {
            return;
        }
        using var pool = ObjC.Pool();
        // System Settings › Desktop & Dock › "Double-click a window's title bar to".
        var defaults = ObjC.Send(ObjC.Class("NSUserDefaults"), "standardUserDefaults");
        var action = ObjC.ToManagedString(ObjC.Send(defaults, "stringForKey:", ObjC.String("AppleActionOnDoubleClick")));
        switch (action)
        {
            case "Minimize":
                ObjC.Send(window, "performMiniaturize:", 0);
                break;
            case "None":
                break;
            default:
                ObjC.Send(window, "performZoom:", 0);
                break;
        }
    }
}
