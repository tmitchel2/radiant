using System;

namespace Radiant.Platform.MacOS;

/// <summary>
/// Cursors through <c>NSCursor</c>'s shared system cursors.
/// <para>
/// <c>[NSCursor set]</c> lasts until something else sets the cursor. GLFW's content view sets
/// the arrow whenever the pointer re-enters it (<c>cursorUpdate:</c>), so
/// <see cref="MacContentView"/> sends those events here and <see cref="Apply"/> puts the UI's
/// shape back; without a window the shape simply holds until the pointer leaves.
/// </para>
/// </summary>
internal sealed class MacCursorService : ICursorService
{
    private readonly nint[] _cursors = new nint[Enum.GetValues<CursorShape>().Length];

    /// <inheritdoc/>
    public CursorShape Current { get; private set; }

    /// <inheritdoc/>
    public void Show(CursorShape shape)
    {
        Current = shape;
        Apply();
    }

    /// <summary>Shows <see cref="Current"/> again, after AppKit or GLFW set another.</summary>
    public void Apply()
    {
        using var pool = ObjC.Pool();
        ObjC.Send(Cursor(Current), "set");
    }

    /// <summary>
    /// The <c>NSCursor</c> for a shape: the system's shared instance, looked up once. A shape this
    /// macOS lacks is the arrow.
    /// </summary>
    public nint Cursor(CursorShape shape)
    {
        var index = (int)shape;
        if ((uint)index >= (uint)_cursors.Length)
        {
            shape = CursorShape.Arrow;
            index = 0;
        }
        if (_cursors[index] == 0)
        {
            using var pool = ObjC.Pool();
            var cls = ObjC.Class("NSCursor");
            var selector = Selector(shape);
            var cursor = ObjC.RespondsTo(cls, selector) ? ObjC.Send(cls, selector) : 0;
            // Retained, as the cache outlives any autorelease pool (they're shared and immortal anyway).
            _cursors[index] = ObjC.Send(cursor != 0 ? cursor : ObjC.Send(cls, "arrowCursor"), "retain");
        }
        return _cursors[index];
    }

    /// <summary>The <c>NSCursor</c> class method that returns each shape.</summary>
    public static string Selector(CursorShape shape) => shape switch
    {
        CursorShape.IBeam => "IBeamCursor",
        CursorShape.PointingHand => "pointingHandCursor",
        CursorShape.ResizeLeftRight => "resizeLeftRightCursor",
        CursorShape.ResizeUpDown => "resizeUpDownCursor",
        CursorShape.Crosshair => "crosshairCursor",
        CursorShape.NotAllowed => "operationNotAllowedCursor",
        CursorShape.Grab => "openHandCursor",
        CursorShape.Grabbing => "closedHandCursor",
        CursorShape.ResizeLeft => "resizeLeftCursor",
        CursorShape.ResizeRight => "resizeRightCursor",
        CursorShape.ResizeUp => "resizeUpCursor",
        CursorShape.ResizeDown => "resizeDownCursor",
        CursorShape.IBeamVertical => "IBeamCursorForVerticalLayout",
        CursorShape.DragCopy => "dragCopyCursor",
        CursorShape.DragLink => "dragLinkCursor",
        CursorShape.ContextMenu => "contextualMenuCursor",
        CursorShape.Disappearing => "disappearingItemCursor",
        _ => "arrowCursor",
    };
}
