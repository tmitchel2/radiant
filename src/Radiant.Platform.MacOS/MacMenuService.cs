using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Radiant.Platform.MacOS;

/// <summary>
/// Context menus as <c>NSMenu</c>s popped up in the content view: each item's action goes to a
/// small target object that notes the item's tag, and <c>popUpMenuPositioningItem:atLocation:inView:</c>
/// runs AppKit's menu loop until the user chooses or dismisses it.
/// </summary>
internal sealed unsafe class MacMenuService(nint view) : IMenuService
{
    private const string TargetClass = "RadiantMenuTarget";

    // The chosen item's tag, set by the target during the menu loop; -1 for none.
    [System.ThreadStatic]
    private static nint s_chosen;

    /// <inheritdoc/>
    public bool IsSupported => view != 0;

    /// <inheritdoc/>
    public int? ShowContextMenu(IReadOnlyList<PlatformMenuItem> items, Vector2 position)
    {
        System.ArgumentNullException.ThrowIfNull(items);
        if (view == 0)
        {
            return null;
        }
        using var pool = ObjC.Pool();
        var cls = ObjC.DefineClass(TargetClass, ("radiantChoose:", (nint)(delegate* unmanaged<nint, nint, nint, void>)&Choose, "v@:@"));
        var target = ObjC.Send(ObjC.Send(cls, "alloc"), "init");
        var menu = ObjC.Send(ObjC.Send(ObjC.Class("NSMenu"), "alloc"), "initWithTitle:", ObjC.String(""));
        // The items say whether they're enabled; AppKit shouldn't decide it from the target.
        ObjC.SendBool(menu, "setAutoenablesItems:", false);
        for (var i = 0; i < items.Count; i++)
        {
            var entry = items[i];
            if (entry.IsSeparator)
            {
                ObjC.Send(menu, "addItem:", ObjC.Send(ObjC.Class("NSMenuItem"), "separatorItem"));
                continue;
            }
            var item = ObjC.Send(ObjC.Send(ObjC.Class("NSMenuItem"), "alloc"), "initWithTitle:action:keyEquivalent:",
                ObjC.String(entry.Title), ObjC.Sel("radiantChoose:"), ObjC.String(""));
            ObjC.Send(item, "setTarget:", target);
            ObjC.Send(item, "setTag:", i);
            ObjC.SendBool(item, "setEnabled:", entry.Enabled);
            ObjC.Send(item, "setState:", entry.Checked ? 1 : 0);
            ObjC.Send(menu, "addItem:", item);
            ObjC.Send(item, "release");
        }

        // The view's coordinates run up from its bottom unless it's flipped; the UI's run down.
        var height = ObjC.GetRect(view, "bounds").Height;
        var y = ObjC.GetBool(view, "isFlipped") ? position.Y : height - position.Y;
        s_chosen = -1;
        ObjC.GetBool(menu, "popUpMenuPositioningItem:atLocation:inView:", 0, position.X, y, view);
        var chosen = s_chosen;
        ObjC.Send(menu, "release");
        ObjC.Send(target, "release");
        return chosen >= 0 ? (int)chosen : null;
    }

    [UnmanagedCallersOnly]
    private static void Choose(nint self, nint cmd, nint sender) => s_chosen = ObjC.Send(sender, "tag");
}
