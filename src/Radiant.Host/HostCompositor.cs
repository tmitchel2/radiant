using System.Numerics;
using Radiant.Graphics2D;

namespace Radiant.Host;

/// <summary>
/// While a tab is being dragged for an in-strip reorder, the dragged tab is lifted out of its slot and
/// the remaining tabs pack together, leaving a one-cell gap (a placeholder) where it would land — so it
/// reads as relocating, not duplicating.
/// </summary>
/// <param name="SourceIndex">Strip index of the dragged tab (omitted from its slot).</param>
/// <param name="InsertionGap">The drop gap (0..tab count) the placeholder sits at.</param>
/// <param name="DraggedLabel">The dragged tab's label, shown on the placeholder.</param>
internal readonly record struct ReorderPreview(int SourceIndex, int InsertionGap, string DraggedLabel);

/// <summary>
/// While a tab from <i>another</i> host is dragged over this host's strip (a cross-window merge), the
/// strip is drawn as it will look once the merge commits: the existing tabs reflow to make room and the
/// incoming tab appears as a real (active-styled) cell at the drop gap — replacing the older translucent
/// placeholder, so the merge previews as already-done before release.
/// </summary>
/// <param name="InsertionGap">The drop gap (0..existing tab count) the incoming cell sits at.</param>
/// <param name="Label">The incoming tab's label, shown on its cell.</param>
internal readonly record struct IncomingPreview(int InsertionGap, string Label);

/// <summary>
/// Draws the host's composite for one frame: the active tab's rendered image filling the area below
/// a tab strip, with the strip (background + per-tab cells + labels) on top. Pure draw logic over a
/// <see cref="Renderer2D"/>, shared by the off-screen verification path and the live windowed host.
/// </summary>
internal static class HostCompositor
{
    public const float StripHeight = 28f;

    private static readonly Vector4 s_stripBg = new(0.12f, 0.12f, 0.14f, 1f);
    private static readonly Vector4 s_activeTabBg = new(0.20f, 0.22f, 0.28f, 1f);
    private static readonly Vector4 s_inactiveTabBg = new(0.14f, 0.14f, 0.17f, 1f);
    private static readonly Vector4 s_activeText = new(0.95f, 0.96f, 1f, 1f);
    private static readonly Vector4 s_inactiveText = new(0.6f, 0.62f, 0.7f, 1f);
    private static readonly Vector4 s_tabSeparator = new(0.08f, 0.08f, 0.10f, 1f);
    private static readonly Vector4 s_dragGhostBg = new(0.20f, 0.34f, 0.60f, 0.78f);
    private static readonly Vector4 s_insertionMarker = new(0.45f, 0.62f, 1f, 1f);
    private static readonly Vector4 s_placeholderBg = new(0.30f, 0.42f, 0.66f, 0.55f);
    private static readonly Vector4 s_placeholderBorder = new(0.45f, 0.62f, 1f, 0.9f);
    private static readonly Vector4 s_closeGlyph = new(0.70f, 0.72f, 0.80f, 1f);
    private static readonly Vector4 s_plusGlyph = new(0.70f, 0.72f, 0.80f, 1f);

    /// <summary>
    /// Encode the composite. The renderer's <c>BeginFrame</c> must already have been called; the
    /// caller ends the frame into its target. <paramref name="frame"/> may be null (no active frame
    /// yet) — then only the strip is drawn.
    /// </summary>
    public static void Draw(
        Renderer2D renderer,
        Texture2D? frame,
        int viewWidth,
        int viewHeight,
        IReadOnlyList<string> tabLabels,
        int activeIndex,
        int hoveredIndex,
        ReorderPreview? reorder,
        float framePixelScale,
        MsdfFont? font,
        IncomingPreview? incoming = null)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(tabLabels);

        var bodyTop = StripHeight;
        var bodyHeight = viewHeight - StripHeight;

        // Body: the active tab's subprocess frame, below the strip. Drawn at its NATIVE 1:1 logical size
        // (frame physical / pixel scale), anchored top-left — NOT stretched to the content rect. The
        // subprocess can't match the host's resize rate, so stretching its stale-sized frame to the live
        // content rect makes the scene + UI panels jitter while dragging. Native-1:1 keeps them crisp and
        // fixed; the freshly-exposed edge is briefly background until the subprocess catches up (in steady
        // state its frame matches the content rect, so this is identical then). framePixelScale <= 0 falls
        // back to stretch-to-fill (used by the off-screen compose test, which has no live scale).
        if (frame != null && bodyHeight > 0)
        {
            var imgW = framePixelScale > 0f ? frame.Width / framePixelScale : viewWidth;
            var imgH = framePixelScale > 0f ? frame.Height / framePixelScale : bodyHeight;
            renderer.DrawImage(frame, 0, bodyTop, imgW, imgH);
        }

        // Tab strip background.
        renderer.DrawRectangleFilled(0, 0, viewWidth, StripHeight, s_stripBg);

        // Equal-width tab cells (capped width) — geometry from the shared TabStripLayout. An incoming
        // cross-window merge previews one extra cell, so the strip (cell widths + the (+) button position)
        // is laid out for the post-merge tab count.
        var stripCount = incoming is not null ? tabLabels.Count + 1 : tabLabels.Count;
        var layout = new TabStripLayout(viewWidth, stripCount);

        // New-tab (+) button at the right of the strip (drawn for any tab count).
        DrawPlusGlyph(renderer, layout.PlusRect);

        // While reordering, render the packed preview (dragged tab lifted out, gap = placeholder) so it
        // reads as relocating rather than duplicating.
        if (reorder is { } r && r.SourceIndex >= 0 && r.SourceIndex < tabLabels.Count)
        {
            DrawReorderPreview(renderer, layout, tabLabels, r, font);
            return;
        }

        // While a tab from another host hovers our strip (cross-window merge), render the incoming tab as a
        // real active cell with the existing tabs reflowed around it — the live preview of the merged strip.
        if (incoming is { } inc)
        {
            DrawIncomingPreview(renderer, layout, tabLabels, inc, font);
            return;
        }

        for (var i = 0; i < tabLabels.Count; i++)
        {
            var cell = layout.RectAt(i);
            var isActive = i == activeIndex;
            renderer.DrawRectangleFilled(cell.X, 0, cell.Width, StripHeight, isActive ? s_activeTabBg : s_inactiveTabBg);
            renderer.DrawRectangleFilled(cell.X + cell.Width - 1, 0, 1, StripHeight, s_tabSeparator);

            var closeRect = layout.CloseRectAt(i);
            if (font != null)
            {
                // Always reserve the close-button column in the label width (whether or not the × is drawn
                // this frame) so the label never reflows as hover/active state changes.
                var rightReserve = closeRect is { } cr ? cell.X + cell.Width - cr.X + 4f : 8f;
                var label = Truncate(font, tabLabels[i], cell.Width - 8f - rightReserve);
                renderer.DrawText(font, label, cell.X + 8f, 7f, 12f, isActive ? s_activeText : s_inactiveText);
            }

            // The close × shows on the active tab and on the hovered tab (Chrome behaviour).
            if (closeRect is { } close && (isActive || i == hoveredIndex))
            {
                DrawCloseGlyph(renderer, close);
            }
        }
    }

    /// <summary>
    /// Draw the strip mid-reorder: the dragged tab is omitted from its slot, the remaining tabs pack
    /// left-to-right, and a placeholder occupies the gap where the dragged tab would land. The placeholder's
    /// reduced-list slot is the drop gap adjusted for the removed tab (matching <see cref="TabController.Reorder"/>).
    /// </summary>
    private static void DrawReorderPreview(
        Renderer2D renderer, TabStripLayout layout, IReadOnlyList<string> tabLabels, ReorderPreview r, MsdfFont? font)
    {
        var count = tabLabels.Count;
        // Removing the dragged tab shifts a drop gap past it left by one (same as Reorder's index fixup).
        var placeholderSlot = Math.Clamp(r.InsertionGap > r.SourceIndex ? r.InsertionGap - 1 : r.InsertionGap, 0, count - 1);
        var reducedIndex = 0;
        for (var slot = 0; slot < count; slot++)
        {
            if (slot == placeholderSlot)
            {
                DrawTabPlaceholder(renderer, layout, slot, r.DraggedLabel, font);
                continue;
            }
            // The next non-dragged tab in order (skip the source).
            if (reducedIndex == r.SourceIndex)
            {
                reducedIndex++;
            }
            var cell = layout.RectAt(slot);
            renderer.DrawRectangleFilled(cell.X, 0, cell.Width, StripHeight, s_inactiveTabBg);
            renderer.DrawRectangleFilled(cell.X + cell.Width - 1, 0, 1, StripHeight, s_tabSeparator);
            if (font != null)
            {
                renderer.DrawText(font, Truncate(font, tabLabels[reducedIndex], cell.Width - 16f), cell.X + 8f, 7f, 12f, s_inactiveText);
            }
            reducedIndex++;
        }
    }

    /// <summary>
    /// Draw the strip mid cross-window merge: the existing tabs reflow across an <c>N+1</c>-cell layout and
    /// the incoming tab occupies the drop gap as a real active-styled cell (highlighted border), so the
    /// strip previews exactly as it will look once the merge commits (<see cref="TabController.AdoptTab"/>
    /// inserts at the same gap and activates). Mirrors <see cref="DrawReorderPreview"/> but inserts a cell
    /// rather than relocating one.
    /// </summary>
    private static void DrawIncomingPreview(
        Renderer2D renderer, TabStripLayout layout, IReadOnlyList<string> tabLabels, IncomingPreview inc, MsdfFont? font)
    {
        var count = tabLabels.Count;
        var gap = Math.Clamp(inc.InsertionGap, 0, count);
        var existingIndex = 0;
        for (var slot = 0; slot <= count; slot++)
        {
            var cell = layout.RectAt(slot);
            if (slot == gap)
            {
                // The incoming tab: active-styled fill + a highlighted border to read as "landing here".
                renderer.DrawRectangleFilled(cell.X, 0, cell.Width, StripHeight, s_activeTabBg);
                renderer.DrawRectangleOutline(cell.X, 0, cell.Width, StripHeight, s_placeholderBorder);
                if (font != null)
                {
                    renderer.DrawText(font, Truncate(font, inc.Label, cell.Width - 16f), cell.X + 8f, 7f, 12f, s_activeText);
                }
                continue;
            }
            renderer.DrawRectangleFilled(cell.X, 0, cell.Width, StripHeight, s_inactiveTabBg);
            renderer.DrawRectangleFilled(cell.X + cell.Width - 1, 0, 1, StripHeight, s_tabSeparator);
            if (font != null)
            {
                renderer.DrawText(font, Truncate(font, tabLabels[existingIndex], cell.Width - 16f), cell.X + 8f, 7f, 12f, s_inactiveText);
            }
            existingIndex++;
        }
    }

    /// <summary>Draw a new-tab (+) glyph centred in the strip within the plus-button rect.</summary>
    private static void DrawPlusGlyph(Renderer2D renderer, TabRect plus)
    {
        const float ArmHalf = 5f;
        var cx = plus.X + plus.Width / 2f;
        var cy = StripHeight / 2f;
        renderer.DrawLine(new Vector2(cx - ArmHalf, cy), new Vector2(cx + ArmHalf, cy), s_plusGlyph);
        renderer.DrawLine(new Vector2(cx, cy - ArmHalf), new Vector2(cx, cy + ArmHalf), s_plusGlyph);
    }

    /// <summary>Draw a close (×) glyph centred in the strip within the close-button rect.</summary>
    private static void DrawCloseGlyph(Renderer2D renderer, TabRect close)
    {
        const float Inset = 3.5f;
        var left = close.X + Inset;
        var right = close.X + close.Width - Inset;
        var cy = StripHeight / 2f;
        var half = (close.Width - 2f * Inset) / 2f;
        renderer.DrawLine(new Vector2(left, cy - half), new Vector2(right, cy + half), s_closeGlyph);
        renderer.DrawLine(new Vector2(left, cy + half), new Vector2(right, cy - half), s_closeGlyph);
    }

    /// <summary>
    /// Draw a vertical insertion marker at the gap <paramref name="gapIndex"/> (0..tab count) while a
    /// drag-to-reorder hovers inside the strip, showing where the dragged tab would land.
    /// </summary>
    public static void DrawInsertionIndicator(Renderer2D renderer, TabStripLayout layout, int gapIndex)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(layout);
        const float MarkerWidth = 2f;
        var x = gapIndex * layout.TabWidth - MarkerWidth / 2f;
        renderer.DrawRectangleFilled(MathF.Max(x, 0f), 0, MarkerWidth, StripHeight, s_insertionMarker);
    }

    /// <summary>
    /// Draw a full cell-sized drop placeholder at the insertion gap <paramref name="gapIndex"/>
    /// (0..tab count) — the "potential tab" shown in the strip where the dragged tab would land, replacing
    /// the floating thumbnail while the cursor is over a strip. Drawn after <see cref="Draw"/>.
    /// </summary>
    public static void DrawTabPlaceholder(Renderer2D renderer, TabStripLayout layout, int gapIndex, string label, MsdfFont? font)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(layout);
        var width = layout.TabWidth;
        if (width <= 0f)
        {
            return;
        }
        // Clamp the slot so the last gap (past the final cell) still renders fully within the strip.
        var x = MathF.Min(gapIndex * width, layout.TabCount * width);
        renderer.DrawRectangleFilled(x, 0, width, StripHeight, s_placeholderBg);
        renderer.DrawRectangleOutline(x, 0, width, StripHeight, s_placeholderBorder);
        if (font != null)
        {
            renderer.DrawText(font, Truncate(font, label, width - 16f), x + 8f, 7f, 12f, s_activeText);
        }
    }

    /// <summary>
    /// Draw a tab drag-ghost (a small labelled chip centred on <paramref name="pos"/>) while a tear-off
    /// drag is in progress. Drawn after <see cref="Draw"/> within the same frame.
    /// </summary>
    public static void DrawDragGhost(Renderer2D renderer, Vector2 pos, MsdfFont? font, string label)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        const float Width = 140f;
        const float Height = 22f;
        var x = pos.X - Width / 2f;
        var y = pos.Y - Height / 2f;
        renderer.DrawRectangleFilled(x, y, Width, Height, s_dragGhostBg);
        if (font != null)
        {
            renderer.DrawText(font, Truncate(font, label, Width - 16f), x + 8f, y + 4f, 12f, s_activeText);
        }
    }

    private static string Truncate(MsdfFont font, string text, float maxWidth)
    {
        if (Renderer2D.MeasureText(font, text, 12f) <= maxWidth)
        {
            return text;
        }
        const string Ellipsis = "…";
        for (var len = text.Length - 1; len > 0; len--)
        {
            var candidate = text[..len] + Ellipsis;
            if (Renderer2D.MeasureText(font, candidate, 12f) <= maxWidth)
            {
                return candidate;
            }
        }
        return Ellipsis;
    }
}
