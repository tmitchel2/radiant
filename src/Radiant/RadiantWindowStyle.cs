namespace Radiant
{
    /// <summary>
    /// Optional window styling for special-purpose windows — e.g. a transparent, borderless,
    /// always-on-top, click-through overlay used to float a dragged tab across the screen. Defaults
    /// match an ordinary opaque, decorated, focusable window so existing callers are unaffected.
    /// </summary>
    public sealed class RadiantWindowStyle
    {
        /// <summary>Whether the window has OS decorations (title bar / border). False = borderless.</summary>
        public bool Decorated { get; init; } = true;

        /// <summary>Whether the framebuffer is transparent (alpha composited over the desktop).</summary>
        public bool Transparent { get; init; }

        /// <summary>Whether the window stays above all others (always-on-top).</summary>
        public bool TopMost { get; init; }

        /// <summary>Whether mouse events pass through to the window beneath (click-through). Best-effort.</summary>
        public bool MousePassthrough { get; init; }

        /// <summary>Whether the window is initially visible.</summary>
        public bool Visible { get; init; } = true;

        /// <summary>
        /// Whether showing the window (incl. a later <c>SetVisible(true)</c>) makes it the focused/key
        /// window. False = show without taking focus — essential for an overlay revealed mid-drag, since
        /// on macOS becoming key window steals focus from the dragging window and delivers it a spurious
        /// mouse-up that terminates the drag. Maps to <c>GLFW_FOCUS_ON_SHOW</c>.
        /// </summary>
        public bool FocusOnShow { get; init; } = true;

        /// <summary>An ordinary opaque, decorated, focusable window.</summary>
        public static RadiantWindowStyle Default { get; } = new();
    }
}
