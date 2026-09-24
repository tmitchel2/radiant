namespace Radiant.Host;

/// <summary>
/// A host window's on-screen rectangle (screen coordinates, logical pixels) plus its tab-strip band
/// height, published so another host can hit-test the global cursor against it during a cross-window
/// drag-merge. Serialized to <c>&lt;data directory&gt;/instances/&lt;host&gt;/window.json</c>.
/// </summary>
internal sealed class WindowBounds
{
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    /// <summary>Height of the tab strip at the top of the window (the preferred drop band for a merge).</summary>
    public float StripHeight { get; set; }

    /// <summary>Whether the screen-space point falls anywhere inside the window.</summary>
    public bool Contains(float screenX, float screenY) =>
        screenX >= X && screenX < X + Width && screenY >= Y && screenY < Y + Height;

    /// <summary>Whether the screen-space point falls inside the tab-strip band at the top of the window.</summary>
    public bool ContainsStrip(float screenX, float screenY) => ContainsStripWithin(screenX, screenY, 0f);

    /// <summary>
    /// Whether the screen-space point falls within <paramref name="margin"/> of the tab-strip band at the
    /// top of the window — the strip rect grown by <paramref name="margin"/> on all sides (so a drag that
    /// is merely <i>near</i> the tabs, especially just below them, still counts as "over the strip" for the
    /// live merge preview and the drop-merge resolution, Chrome-style). <paramref name="margin"/> of 0 is
    /// the strict strip band (<see cref="ContainsStrip"/>).
    /// </summary>
    public bool ContainsStripWithin(float screenX, float screenY, float margin) =>
        screenX >= X - margin && screenX < X + Width + margin
        && screenY >= Y - margin && screenY < Y + StripHeight + margin;
}
