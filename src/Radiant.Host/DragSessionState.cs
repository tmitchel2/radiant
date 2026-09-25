namespace Radiant.Host;

/// <summary>
/// The shared "a tab is being dragged" state the source host publishes and the floating drag-overlay
/// process consumes. Written to <c>&lt;data directory&gt;/drag.json</c>. When <see cref="Active"/> is false the
/// overlay hides; otherwise it floats the dragged tab's frame at (<see cref="CursorX"/>,
/// <see cref="CursorY"/>) in screen coordinates.
/// </summary>
internal sealed class DragSessionState
{
    /// <summary>Whether a drag is in progress (false = overlay hidden).</summary>
    public bool Active { get; set; }

    /// <summary>Global cursor x (screen coordinates, logical pixels).</summary>
    public float CursorX { get; set; }

    /// <summary>Global cursor y (screen coordinates, logical pixels).</summary>
    public float CursorY { get; set; }

    /// <summary>Instance name of the dragged renderer (for diagnostics).</summary>
    public string TabName { get; set; } = "";

    /// <summary>Path to the dragged renderer's frame buffer (diagnostics only — the overlay must not open
    /// it, as it is single-consumer and read by the source host as the active tab).</summary>
    public string FramesPath { get; set; } = "";

    /// <summary>Path to the host-published thumbnail buffer the overlay reads to show a live downscaled
    /// frame of the dragged tab. Empty when no thumbnail is available (overlay falls back to the chip).</summary>
    public string ThumbnailPath { get; set; } = "";

    /// <summary>Instance name of the host that started the drag.</summary>
    public string SourceHost { get; set; } = "";

    /// <summary>The single host whose tab strip the drag driver resolved the cursor to be over (within the
    /// proximity margin) this frame — authoritative, so exactly one host shows the live merge preview /
    /// opens the preview buffer even when windows overlap. Empty when over no strip. See
    /// <see cref="PreviewPath"/>.</summary>
    public string TargetHost { get; set; } = "";

    /// <summary>Path to the host-owned full-resolution merge-preview buffer the <see cref="TargetHost"/>
    /// reads to composite the dragged tab's live content while it hovers (a clean second
    /// single-producer/single-consumer channel, never the renderer's single-consumer frame buffer). Owned
    /// by the content host — the source for a direct merge, the <see cref="FollowHost"/> once torn off.
    /// Empty when not over a strip (no preview).</summary>
    public string PreviewPath { get; set; } = "";

    /// <summary>The dragged tab's label (drawn on the ghost when no frame is available).</summary>
    public string Label { get; set; } = "";

    /// <summary>Whether the cursor is currently over some host's tab strip. When true the floating overlay
    /// hides and the strip-owning host draws an in-strip drop placeholder instead (Chrome behaviour).</summary>
    public bool OverStrip { get; set; }

    /// <summary>When set, the tab has been torn off mid-drag into this host, which should follow the cursor
    /// until the drag ends. Empty until a live tear-off fires. See <see cref="TornOff"/>.</summary>
    public string FollowHost { get; set; } = "";

    /// <summary>Whether the live tear-off has fired (the dragged tab is now its own following window). The
    /// floating overlay hides once true — the real following window <see cref="FollowHost"/> is the feedback.</summary>
    public bool TornOff { get; set; }

    /// <summary>Window-local x of the grab point, kept under the cursor as the torn-off window follows
    /// (<c>MoveWindow(cursorX − GrabOffsetX, …)</c>), so the window doesn't teleport its origin to the cursor.</summary>
    public float GrabOffsetX { get; set; }

    /// <summary>Window-local y of the grab point (see <see cref="GrabOffsetX"/>).</summary>
    public float GrabOffsetY { get; set; }
}
