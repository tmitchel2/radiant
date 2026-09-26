using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Radiant.Platform.MacOS;

/// <summary>
/// Context menus as <c>NSMenu</c>s popped up in the content view: each item's action goes to a
/// small target object that notes the item's tag, and <c>popUpMenuPositioningItem:atLocation:inView:</c>
/// runs AppKit's menu loop until the user chooses or dismisses it. The menu bar is the
/// application's main menu, rebuilt whole each time it's set: the application menu (Hide, Quit),
/// the app's menus, and a Window menu unless the app has one.
/// </summary>
internal sealed unsafe class MacMenuService(nint view) : IMenuService
{
    private const string TargetClass = "RadiantMenuTarget";

    // The chosen item's tag, set by the target during the menu loop; -1 for none.
    [System.ThreadStatic]
    private static nint s_chosen;

    private const string MenuBarTargetClass = "RadiantMenuBarTarget";

    // The menu bar's items all target one object, kept for the life of the process; a menu item
    // doesn't retain its target. Its choices go to the last SetMenuBar's callback.
    private static nint s_menuBarTarget;
    private static Action<int, int>? s_onMenuBarChoose;

    /// <inheritdoc/>
    public bool IsSupported => view != 0;

    /// <inheritdoc/>
    public bool HasMenuBar => view != 0;

    /// <inheritdoc/>
    public void SetMenuBar(IReadOnlyList<PlatformMenu> menus, Action<int, int> onChoose)
    {
        ArgumentNullException.ThrowIfNull(menus);
        ArgumentNullException.ThrowIfNull(onChoose);
        if (view == 0)
        {
            return;
        }
        using var pool = ObjC.Pool();
        s_onMenuBarChoose = onChoose;
        if (s_menuBarTarget == 0)
        {
            var cls = ObjC.DefineClass(MenuBarTargetClass, ("radiantMenuBarChoose:", (nint)(delegate* unmanaged<nint, nint, nint, void>)&ChooseFromMenuBar, "v@:@"));
            s_menuBarTarget = ObjC.Send(ObjC.Send(cls, "alloc"), "init");
        }
        var app = ObjC.Send(ObjC.Class("NSApplication"), "sharedApplication");
        var name = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        var main = NewMenu("");

        // The application menu: its title is always the app's name, whatever the item says.
        var appMenu = NewMenu(name);
        AddItem(appMenu, $"Hide {name}", "hide:", 0, "h", Command);
        AddItem(appMenu, "Hide Others", "hideOtherApplications:", 0, "h", Command | Option);
        AddItem(appMenu, "Show All", "unhideAllApplications:", 0, "", 0);
        ObjC.Send(appMenu, "addItem:", ObjC.Send(ObjC.Class("NSMenuItem"), "separatorItem"));
        AddItem(appMenu, $"Quit {name}", "terminate:", 0, "q", Command);
        AddSubmenu(main, name, appMenu);

        var hasWindowMenu = false;
        for (var m = 0; m < menus.Count; m++)
        {
            var menu = NewMenu(menus[m].Title);
            ObjC.SendBool(menu, "setAutoenablesItems:", false);
            var items = menus[m].Items;
            for (var i = 0; i < items.Count; i++)
            {
                var entry = items[i];
                if (entry.IsSeparator)
                {
                    ObjC.Send(menu, "addItem:", ObjC.Send(ObjC.Class("NSMenuItem"), "separatorItem"));
                    continue;
                }
                var (key, mask) = KeyEquivalent(entry.Shortcut);
                var item = AddItem(menu, entry.Title, "radiantMenuBarChoose:", s_menuBarTarget, key, mask);
                ObjC.Send(item, "setTag:", (m << 16) | i);
                ObjC.SendBool(item, "setEnabled:", entry.Enabled);
                ObjC.Send(item, "setState:", entry.Checked ? 1 : 0);
            }
            AddSubmenu(main, menus[m].Title, menu);
            if (menus[m].Title == "Window")
            {
                ObjC.Send(app, "setWindowsMenu:", menu);
                hasWindowMenu = true;
            }
        }
        if (!hasWindowMenu)
        {
            var window = NewMenu("Window");
            AddItem(window, "Minimize", "performMiniaturize:", 0, "m", Command);
            AddItem(window, "Zoom", "performZoom:", 0, "", 0);
            AddSubmenu(main, "Window", window);
            ObjC.Send(app, "setWindowsMenu:", window);
        }
        // The application keeps the menu; ours was autoreleased when it was made (NewMenu).
        ObjC.Send(app, "setMainMenu:", main);
    }

    // NSEventModifierFlags.
    private const int Shift = 1 << 17;
    private const int Control = 1 << 18;
    private const int Option = 1 << 19;
    private const int Command = 1 << 20;

    private static nint NewMenu(string title) =>
        ObjC.Send(ObjC.Send(ObjC.Send(ObjC.Class("NSMenu"), "alloc"), "initWithTitle:", ObjC.String(title)), "autorelease");

    // Adds an item whose action is sent to the target (or, with none, along the responder chain).
    private static nint AddItem(nint menu, string title, string action, nint target, string key, int mask)
    {
        var item = ObjC.Send(ObjC.Send(ObjC.Class("NSMenuItem"), "alloc"), "initWithTitle:action:keyEquivalent:",
            ObjC.String(title), ObjC.Sel(action), ObjC.String(key));
        if (key.Length > 0)
        {
            ObjC.Send(item, "setKeyEquivalentModifierMask:", mask);
        }
        if (target != 0)
        {
            ObjC.Send(item, "setTarget:", target);
        }
        ObjC.Send(menu, "addItem:", item);
        ObjC.Send(item, "release");
        return item;
    }

    private static void AddSubmenu(nint main, string title, nint menu)
    {
        var holder = ObjC.Send(ObjC.Send(ObjC.Class("NSMenuItem"), "alloc"), "initWithTitle:action:keyEquivalent:",
            ObjC.String(title), 0, ObjC.String(""));
        ObjC.Send(holder, "setSubmenu:", menu);
        ObjC.Send(main, "addItem:", holder);
        ObjC.Send(holder, "release");
    }

    // A shortcut as AppKit's key equivalent: the character the key types, or the function-key
    // character for the others, and the modifier mask.
    private static (string Key, int Mask) KeyEquivalent(MenuShortcut? shortcut)
    {
        if (shortcut is null)
        {
            return ("", 0);
        }
        var key = shortcut.Key switch
        {
            "Enter" => "\r",
            "Escape" => "\u001b",
            "Tab" => "\t",
            "Space" => " ",
            "Backspace" => "\b",
            "Delete" => "\uf728",
            "Up" => "\uf700",
            "Down" => "\uf701",
            "Left" => "\uf702",
            "Right" => "\uf703",
            "Home" => "\uf729",
            "End" => "\uf72b",
            "PageUp" => "\uf72c",
            "PageDown" => "\uf72d",
            ['F', .. var n] when int.TryParse(n, out var f) && f is >= 1 and <= 35 => ((char)(0xF704 + f - 1)).ToString(),
            _ => shortcut.Key.ToLowerInvariant(),
        };
        var held = shortcut.Modifiers;
        var mask = ((held & MenuModifiers.Shift) != 0 ? Shift : 0) | ((held & MenuModifiers.Control) != 0 ? Control : 0)
            | ((held & MenuModifiers.Alt) != 0 ? Option : 0) | ((held & MenuModifiers.Command) != 0 ? Command : 0);
        return (key, mask);
    }

    [UnmanagedCallersOnly]
    private static void ChooseFromMenuBar(nint self, nint cmd, nint sender)
    {
        var tag = (int)ObjC.Send(sender, "tag");
        s_onMenuBarChoose?.Invoke(tag >> 16, tag & 0xFFFF);
    }

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
