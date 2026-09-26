namespace Radiant.Platform;

/// <summary>
/// The pointer's shape. The first group is what every desktop platform has; the rest are macOS
/// cursors that cost nothing to offer, and fall back to <see cref="Arrow"/> where a platform
/// lacks them.
/// </summary>
public enum CursorShape
{
    /// <summary>The ordinary pointer.</summary>
    Arrow,

    /// <summary>Over text that can be selected or edited.</summary>
    IBeam,

    /// <summary>Over a link or something that acts when clicked.</summary>
    PointingHand,

    /// <summary>Over something that resizes horizontally, such as a column divider.</summary>
    ResizeLeftRight,

    /// <summary>Over something that resizes vertically, such as a row divider.</summary>
    ResizeUpDown,

    /// <summary>Precise picking, such as a colour picker or a drawing tool.</summary>
    Crosshair,

    /// <summary>The action under the pointer isn't possible, such as a drop target that won't accept.</summary>
    NotAllowed,

    /// <summary>Over something that can be dragged (an open hand).</summary>
    Grab,

    /// <summary>While dragging something (a closed hand).</summary>
    Grabbing,

    /// <summary>A resize edge that can only move left.</summary>
    ResizeLeft,

    /// <summary>A resize edge that can only move right.</summary>
    ResizeRight,

    /// <summary>A resize edge that can only move up.</summary>
    ResizeUp,

    /// <summary>A resize edge that can only move down.</summary>
    ResizeDown,

    /// <summary>Over vertical text.</summary>
    IBeamVertical,

    /// <summary>A drag that will copy.</summary>
    DragCopy,

    /// <summary>A drag that will make a link or alias.</summary>
    DragLink,

    /// <summary>Over something with a context menu.</summary>
    ContextMenu,

    /// <summary>A dragged item will disappear if dropped here (macOS's puff of smoke).</summary>
    Disappearing,
}
