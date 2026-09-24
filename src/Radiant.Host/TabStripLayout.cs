namespace Radiant.Host;

/// <summary>
/// The position + width of one tab cell in the strip, in strip-local pixels (origin at the strip's
/// top-left, x growing right).
/// </summary>
internal readonly record struct TabRect(float X, float Width);

/// <summary>
/// Pure geometry of the tab strip: equal-width cells, capped at <see cref="MaxTabWidth"/>, laid out
/// left-to-right. Centralises the strip math that the live host (click → tab index) and the compositor
/// (cell rectangles) both need, plus the insertion-gap math drag-to-reorder needs. Side-effect-free so
/// it can be unit-tested without a window.
/// </summary>
internal sealed class TabStripLayout
{
    /// <summary>Maximum width of a single tab cell; below this, cells share the view width equally.</summary>
    public const float MaxTabWidth = 180f;

    /// <summary>Side length of a tab's close (×) button, in strip-local pixels.</summary>
    public const float CloseButtonSize = 14f;

    /// <summary>Gap between a cell's right edge and its close button.</summary>
    public const float CloseButtonMargin = 6f;

    /// <summary>A cell narrower than this hides its close button (no room beside the label).</summary>
    public const float MinWidthForCloseButton = 60f;

    /// <summary>Width of the new-tab (+) button reserved at the right of the strip.</summary>
    public const float PlusButtonWidth = 28f;

    public TabStripLayout(float viewWidth, int tabCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tabCount);
        TabCount = tabCount;
        // Reserve the new-tab (+) button's width so tabs never sit under it; cells share the remainder.
        var available = MathF.Max(0f, viewWidth - PlusButtonWidth);
        TabWidth = tabCount > 0 ? MathF.Min(MaxTabWidth, available / tabCount) : 0f;
    }

    /// <summary>Number of tab cells.</summary>
    public int TabCount { get; }

    /// <summary>Width of each (equal-width) tab cell, or 0 when there are no tabs.</summary>
    public float TabWidth { get; }

    /// <summary>The cell rectangle for <paramref name="index"/> (no bounds check beyond a positive count).</summary>
    public TabRect RectAt(int index) => new(index * TabWidth, TabWidth);

    /// <summary>
    /// The new-tab (+) button rectangle (strip-local x + width), placed immediately right of the last
    /// tab. With cells capped (few wide tabs) it sits just past them; once cells share the full reserved
    /// remainder it lands exactly in the reserved <see cref="PlusButtonWidth"/> column at the strip's right.
    /// </summary>
    public TabRect PlusRect => new(TabCount * TabWidth, PlusButtonWidth);

    /// <summary>
    /// The close-button rectangle (strip-local x + width) inset at the right of cell <paramref name="index"/>,
    /// or null when the cell is too narrow (<see cref="MinWidthForCloseButton"/>) to host one. The strip's
    /// full height is the button's vertical hit band, so only x/width are needed.
    /// </summary>
    public TabRect? CloseRectAt(int index)
    {
        var cell = RectAt(index);
        if (cell.Width < MinWidthForCloseButton)
        {
            return null;
        }
        var x = cell.X + cell.Width - CloseButtonSize - CloseButtonMargin;
        return new TabRect(x, CloseButtonSize);
    }

    /// <summary>
    /// The tab index under strip-local x, or -1 when x falls outside any cell. Used for click-to-select
    /// and to pick the tab a drag started on.
    /// </summary>
    public int HitTest(float x)
    {
        if (TabCount == 0 || TabWidth <= 0f || x < 0f)
        {
            return -1;
        }
        var idx = (int)(x / TabWidth);
        return idx >= 0 && idx < TabCount ? idx : -1;
    }

    /// <summary>
    /// The insertion gap (0..<see cref="TabCount"/>) nearest strip-local x: the index a dragged tab
    /// would land at if dropped here. Gap i sits to the left of cell i; gap <see cref="TabCount"/> is
    /// past the last cell. Computed by snapping to the nearest cell boundary (cell midpoints split gaps).
    /// </summary>
    public int InsertionIndexAt(float x)
    {
        if (TabCount == 0 || TabWidth <= 0f)
        {
            return 0;
        }
        var gap = (int)MathF.Floor((x + TabWidth / 2f) / TabWidth);
        return Math.Clamp(gap, 0, TabCount);
    }
}
