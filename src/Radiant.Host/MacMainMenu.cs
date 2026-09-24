using System.Runtime.InteropServices;
using Radiant.Host.Ipc.Recents;

namespace Radiant.Host;

/// <summary>
/// Installs a native macOS menu-bar "File" menu on the compositing host window: New, Open…,
/// Open Recent ▸, Close Tab, Close Window. Built at runtime against GLFW's existing
/// <c>NSApplication</c> delegate using the same AOT-safe pattern as <see cref="MacDockMenu"/> —
/// action selectors added with <c>class_addMethod</c>, backed by <c>[UnmanagedCallersOnly]</c> statics.
///
/// <para>Because those native callbacks cannot capture instance state, the menu actions dispatch through
/// a per-process static delegate registry (<c>s_on*</c>) populated by <see cref="TryInstall"/>. There is
/// exactly one host (one <see cref="LiveHost"/>) per process, so the registry unambiguously targets
/// <em>this</em> window — matching "Close Tab / Close Window act on this window". Menu actions fire on the
/// AppKit main thread (the GLFW run-loop thread that mutates the <see cref="TabController"/> and calls
/// <c>app.Close()</c>), so the delegates call those directly with no cross-thread hand-off.</para>
///
/// <para>Idempotent and exception-safe; a complete no-op off macOS.</para>
/// </summary>
internal static unsafe class MacMainMenu
{
    private static bool s_installed;

    // The app delegate (menu-item target) and the Open Recent submenu, captured so the recent submenu can
    // be rebuilt live and its dynamically-created items can target the delegate.
    private static IntPtr s_delegate;
    private static IntPtr s_recentMenu;

    // Per-process action registry — set by TryInstall, invoked by the [UnmanagedCallersOnly] imps.
    private static Action? s_onNew;
    private static Action? s_onOpen;
    private static Action<string>? s_onOpenPath;
    private static Action? s_onCloseTab;
    private static Action? s_onCloseWindow;

    // NSEventModifierFlagShift (1<<17) | NSEventModifierFlagCommand (1<<20) — for ⌘⇧W (Close Window).
    private const int CmdShiftMask = (1 << 17) | (1 << 20);

    /// <summary>
    /// Best-effort: install the File menu bar. Safe to call every frame — returns immediately once
    /// installed, and silently retries if GLFW's app delegate is not ready yet.
    /// </summary>
    public static void TryInstall(RadiantApplication app, TabController controller)
    {
        if (s_installed || !MacObjc.IsMac)
        {
            return;
        }
#pragma warning disable CA1031 // The menu is non-critical UI; never let installing it crash the host.
        try
        {
            // Wire the action registry (managed closures over this host's app + controller). File ▸ New is
            // the application's to define; without a NewDocument it is left out of the menu.
            var newDocument = RadiantAppIdentity.Current.NewDocument;
            s_onNew = newDocument is null ? null : () => controller.SpawnLocalTab(newDocument());
            s_onOpen = () =>
            {
                var path = MacOpenPanel.Run();
                if (path != null)
                {
                    RecentFilesStore.Add(path);
                    controller.SpawnLocalTab(path);
                }
            };
            s_onOpenPath = path =>
            {
                RecentFilesStore.Add(path);
                controller.SpawnLocalTab(path);
            };
            s_onCloseTab = () => controller.CloseTab(controller.ActiveIndex);
            s_onCloseWindow = app.Close;

            var nsApp = MacObjc.SharedApp();
            if (nsApp == IntPtr.Zero)
            {
                return;
            }
            var del = MacObjc.Send(nsApp, MacObjc.Sel("delegate"));
            if (del == IntPtr.Zero)
            {
                return; // GLFW delegate not set yet — retry next frame.
            }
            var cls = MacObjc.GetClass(del);
            if (cls == IntPtr.Zero)
            {
                return;
            }
            s_delegate = del;

            // Add the five action selectors + the recent-submenu update hook to the delegate class.
            MacObjc.AddMethod(cls, MacObjc.Sel("radiantMenuNew:"),
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&MenuNewImp, "v@:@");
            MacObjc.AddMethod(cls, MacObjc.Sel("radiantMenuOpen:"),
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&MenuOpenImp, "v@:@");
            MacObjc.AddMethod(cls, MacObjc.Sel("radiantMenuOpenRecent:"),
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&MenuOpenRecentImp, "v@:@");
            MacObjc.AddMethod(cls, MacObjc.Sel("radiantMenuCloseTab:"),
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&MenuCloseTabImp, "v@:@");
            MacObjc.AddMethod(cls, MacObjc.Sel("radiantMenuCloseWindow:"),
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&MenuCloseWindowImp, "v@:@");
            MacObjc.AddMethod(cls, MacObjc.Sel("menuNeedsUpdate:"),
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&MenuNeedsUpdateImp, "v@:@");

            BuildMenu(nsApp, del);
            s_installed = true;
        }
        catch
        {
            // Cosmetic only — never propagate.
        }
#pragma warning restore CA1031
    }

    // Build the main menu: a minimal application menu (so File isn't the first menu) + the File menu.
    private static void BuildMenu(IntPtr nsApp, IntPtr target)
    {
        var mainMenu = NewMenu(string.Empty);

        // Application menu (item 0). Quit targets nil so terminate: travels the responder chain to NSApp.
        var appItem = NewItem(string.Empty, IntPtr.Zero, string.Empty, IntPtr.Zero);
        var appMenu = NewMenu(string.Empty);
        var quit = NewItem("Quit", MacObjc.Sel("terminate:"), "q", IntPtr.Zero);
        MacObjc.Send(appMenu, MacObjc.Sel("addItem:"), quit);
        MacObjc.Send(appItem, MacObjc.Sel("setSubmenu:"), appMenu);
        MacObjc.Send(mainMenu, MacObjc.Sel("addItem:"), appItem);

        // File menu.
        var fileItem = NewItem(string.Empty, IntPtr.Zero, string.Empty, IntPtr.Zero);
        var fileMenu = NewMenu("File");

        if (s_onNew is not null)
        {
            MacObjc.Send(fileMenu, MacObjc.Sel("addItem:"),
                NewItem("New", MacObjc.Sel("radiantMenuNew:"), "n", target));
        }
        MacObjc.Send(fileMenu, MacObjc.Sel("addItem:"),
            NewItem("Open…", MacObjc.Sel("radiantMenuOpen:"), "o", target));

        // Open Recent — no action; its submenu is rebuilt on open via menuNeedsUpdate:.
        var recentItem = NewItem("Open Recent", IntPtr.Zero, string.Empty, IntPtr.Zero);
        var recentMenu = NewMenu("Open Recent");
        MacObjc.Send(recentMenu, MacObjc.Sel("setDelegate:"), target);
        MacObjc.Send(recentItem, MacObjc.Sel("setSubmenu:"), recentMenu);
        MacObjc.Send(fileMenu, MacObjc.Sel("addItem:"), recentItem);
        s_recentMenu = recentMenu;

        MacObjc.Send(fileMenu, MacObjc.Sel("addItem:"),
            MacObjc.Send(MacObjc.Cls("NSMenuItem"), MacObjc.Sel("separatorItem")));
        MacObjc.Send(fileMenu, MacObjc.Sel("addItem:"),
            NewItem("Close Tab", MacObjc.Sel("radiantMenuCloseTab:"), "w", target));

        var closeWin = NewItem("Close Window", MacObjc.Sel("radiantMenuCloseWindow:"), "w", target);
        MacObjc.Send(closeWin, MacObjc.Sel("setKeyEquivalentModifierMask:"), (IntPtr)CmdShiftMask);
        MacObjc.Send(fileMenu, MacObjc.Sel("addItem:"), closeWin);

        MacObjc.Send(fileItem, MacObjc.Sel("setSubmenu:"), fileMenu);
        MacObjc.Send(mainMenu, MacObjc.Sel("addItem:"), fileItem);

        MacObjc.Send(nsApp, MacObjc.Sel("setMainMenu:"), mainMenu);
    }

    // An autoreleased NSMenu with the given title (AppKit retains it once installed in the hierarchy).
    private static IntPtr NewMenu(string title)
    {
        var menu = MacObjc.Send(MacObjc.Cls("NSMenu"), MacObjc.Sel("alloc"));
        menu = MacObjc.Send(menu, MacObjc.Sel("initWithTitle:"), MacObjc.NSString(title));
        MacObjc.Send(menu, MacObjc.Sel("autorelease"));
        return menu;
    }

    // An autoreleased NSMenuItem. action == IntPtr.Zero leaves it actionless; target == IntPtr.Zero leaves
    // the responder chain to handle it (used for Quit and for parent items that only carry a submenu).
    private static IntPtr NewItem(string title, IntPtr action, string keyEquivalent, IntPtr target)
    {
        var item = MacObjc.Send(MacObjc.Cls("NSMenuItem"), MacObjc.Sel("alloc"));
        item = MacObjc.Send(item, MacObjc.Sel("initWithTitle:action:keyEquivalent:"),
            MacObjc.NSString(title), action, MacObjc.NSString(keyEquivalent));
        if (target != IntPtr.Zero)
        {
            MacObjc.Send(item, MacObjc.Sel("setTarget:"), target);
        }
        MacObjc.Send(item, MacObjc.Sel("autorelease"));
        return item;
    }

    // ---- [UnmanagedCallersOnly] action callbacks (no managed exception may cross back into AppKit) ----

    [UnmanagedCallersOnly]
    private static void MenuNewImp(IntPtr self, IntPtr cmd, IntPtr sender) => Invoke(s_onNew);

    [UnmanagedCallersOnly]
    private static void MenuOpenImp(IntPtr self, IntPtr cmd, IntPtr sender) => Invoke(s_onOpen);

    [UnmanagedCallersOnly]
    private static void MenuCloseTabImp(IntPtr self, IntPtr cmd, IntPtr sender) => Invoke(s_onCloseTab);

    [UnmanagedCallersOnly]
    private static void MenuCloseWindowImp(IntPtr self, IntPtr cmd, IntPtr sender) => Invoke(s_onCloseWindow);

    [UnmanagedCallersOnly]
    private static void MenuOpenRecentImp(IntPtr self, IntPtr cmd, IntPtr sender)
    {
#pragma warning disable CA1031
        try
        {
            var rep = MacObjc.Send(sender, MacObjc.Sel("representedObject"));
            if (rep == IntPtr.Zero)
            {
                return;
            }
            var path = MacObjc.ReadUtf8(MacObjc.Send(rep, MacObjc.Sel("UTF8String")));
            if (!string.IsNullOrEmpty(path))
            {
                s_onOpenPath?.Invoke(path);
            }
        }
        catch
        {
            // A stale recent entry simply does nothing.
        }
#pragma warning restore CA1031
    }

    // NSMenuDelegate: rebuild the Open Recent submenu each time it is about to open, so it reflects the
    // persisted MRU list without any per-frame work.
    [UnmanagedCallersOnly]
    private static void MenuNeedsUpdateImp(IntPtr self, IntPtr cmd, IntPtr menu)
    {
#pragma warning disable CA1031
        try
        {
            if (menu != s_recentMenu)
            {
                return; // not our submenu — leave it alone
            }
            MacObjc.Send(menu, MacObjc.Sel("removeAllItems"));

            var recent = RecentFilesStore.ListRecent(RecentFilesStore.MaxEntries);
            if (recent.Count == 0)
            {
                var none = NewItem("No Recent Items", IntPtr.Zero, string.Empty, IntPtr.Zero);
                MacObjc.Send(none, MacObjc.Sel("setEnabled:"), false);
                MacObjc.Send(menu, MacObjc.Sel("addItem:"), none);
                return;
            }

            var select = MacObjc.Sel("radiantMenuOpenRecent:");
            foreach (var entry in recent)
            {
                var item = NewItem(DisplayName(entry.Path), select, string.Empty, s_delegate);
                MacObjc.Send(item, MacObjc.Sel("setRepresentedObject:"), MacObjc.NSString(entry.Path));
                MacObjc.Send(menu, MacObjc.Sel("addItem:"), item);
            }
        }
        catch
        {
            // Cosmetic only.
        }
#pragma warning restore CA1031
    }

    private static void Invoke(Action? action)
    {
#pragma warning disable CA1031 // no managed exception may cross back into AppKit
        try
        {
            action?.Invoke();
        }
        catch
        {
            // Swallow — a failed menu action must not tear down the run loop.
        }
#pragma warning restore CA1031
    }

    // A friendly label for a recent entry: the file/folder name (trailing "/" for folders), or the full
    // path when it has no name component.
    private static string DisplayName(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(name))
        {
            return path;
        }
        return Directory.Exists(path) ? name + "/" : name;
    }
}
