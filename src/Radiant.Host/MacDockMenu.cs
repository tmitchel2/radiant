using System.Runtime.InteropServices;
using Radiant.Host.AgentControlProtocol;

namespace Radiant.Host;

/// <summary>
/// Installs a custom macOS Dock menu on the Dock-owner host so right-clicking the application's single Dock
/// tile lists every live host's currently-selected tab, and clicking an entry raises that host's window.
/// This synthesizes, across separate host processes, what a single-process app gets for free (a Dock menu
/// spanning all its windows): the Dock owner enumerates hosts from the instance registry and focuses the
/// chosen one over the existing filesystem command IPC (<c>window.focus</c>).
///
/// <para>GLFW installs its own <c>NSApplication</c> delegate (<c>GLFWApplicationDelegate</c>) which does
/// not implement <c>applicationDockMenu:</c>. We add that method — plus the menu-item action
/// <c>radiantDockSelect:</c> — to the delegate's class at runtime with <c>class_addMethod</c>, backed by
/// <c>[UnmanagedCallersOnly]</c> statics (AOT-safe: static, blittable <c>IntPtr</c> args, and no managed
/// exception ever escapes the native callback). Idempotent and exception-safe; no-op off macOS.</para>
/// </summary>
internal static unsafe class MacDockMenu
{
    private static bool s_installed;

    /// <summary>
    /// Best-effort: make the GLFW app delegate provide the Dock menu. Safe to call every frame — it returns
    /// immediately once installed, and silently retries if the delegate is not ready yet (it normally is by
    /// the first frame, the window having already been created).
    /// </summary>
    public static void TryInstall()
    {
        if (s_installed || !MacObjc.IsMac)
        {
            return;
        }
#pragma warning disable CA1031 // The Dock menu is cosmetic; never let installing it crash the host.
        try
        {
            var app = MacObjc.SharedApp();
            if (app == IntPtr.Zero)
            {
                return;
            }
            var del = MacObjc.Send(app, MacObjc.Sel("delegate"));
            if (del == IntPtr.Zero)
            {
                return; // GLFW delegate not set yet — retry next frame.
            }
            var cls = MacObjc.GetClass(del);
            if (cls == IntPtr.Zero)
            {
                return;
            }

            // applicationDockMenu: returns id, args (self id, _cmd SEL, sender id) → "@@:@".
            var menuAdded = MacObjc.AddMethod(
                cls,
                MacObjc.Sel("applicationDockMenu:"),
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr>)&DockMenuImp,
                "@@:@");
            // radiantDockSelect: returns void, args (self, _cmd, sender NSMenuItem) → "v@:@".
            MacObjc.AddMethod(
                cls,
                MacObjc.Sel("radiantDockSelect:"),
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&DockSelectImp,
                "v@:@");

            if (!menuAdded)
            {
                Console.Error.WriteLine(
                    "Radiant.Host: could not install Dock menu — applicationDockMenu: already present on the app delegate.");
                return;
            }
            s_installed = true;
        }
        catch
        {
            // Cosmetic only — never propagate.
        }
#pragma warning restore CA1031
    }

    // Builds the right-click Dock menu: one item per live host, titled with that host's active tab name,
    // carrying the host name as its representedObject. Returns an autoreleased NSMenu (AppKit retains it).
    [UnmanagedCallersOnly]
    private static IntPtr DockMenuImp(IntPtr self, IntPtr cmd, IntPtr sender)
    {
#pragma warning disable CA1031 // No managed exception may cross back into AppKit.
        try
        {
            var menu = MacObjc.Send(MacObjc.Cls("NSMenu"), MacObjc.Sel("alloc"));
            menu = MacObjc.Send(menu, MacObjc.Sel("init"));
            MacObjc.Send(menu, MacObjc.Sel("autorelease"));

            var select = MacObjc.Sel("radiantDockSelect:");
            var empty = MacObjc.NSString(string.Empty);
            foreach (var info in InstanceRegistry.ListInstances())
            {
                if (Array.IndexOf(info.Capabilities, "tab") < 0)
                {
                    continue; // only hosts (windows), not renderer tabs or the drag overlay
                }
                var label = HostActiveTab.Read(info.Name) ?? info.Name;

                var item = MacObjc.Send(MacObjc.Cls("NSMenuItem"), MacObjc.Sel("alloc"));
                item = MacObjc.Send(item, MacObjc.Sel("initWithTitle:action:keyEquivalent:"),
                    MacObjc.NSString(label), select, empty);
                MacObjc.Send(item, MacObjc.Sel("setTarget:"), self);
                MacObjc.Send(item, MacObjc.Sel("setRepresentedObject:"), MacObjc.NSString(info.Name));
                MacObjc.Send(menu, MacObjc.Sel("addItem:"), item);
                MacObjc.Send(item, MacObjc.Sel("autorelease")); // menu retains it on addItem:; balance our alloc
            }
            return menu;
        }
        catch
        {
            return IntPtr.Zero; // an empty (no) menu is the safe fallback
        }
#pragma warning restore CA1031
    }

    // Menu-item action: focus the host named by the clicked item's representedObject.
    [UnmanagedCallersOnly]
    private static void DockSelectImp(IntPtr self, IntPtr cmd, IntPtr sender)
    {
#pragma warning disable CA1031 // No managed exception may cross back into AppKit.
        try
        {
            var rep = MacObjc.Send(sender, MacObjc.Sel("representedObject"));
            if (rep == IntPtr.Zero)
            {
                return;
            }
            var name = MacObjc.ReadUtf8(MacObjc.Send(rep, MacObjc.Sel("UTF8String")));
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            FocusHostAsync(name);
        }
        catch
        {
            // A stale / unreachable host simply does nothing.
        }
#pragma warning restore CA1031
    }

    // CommandClient.Send polls for a response; dispatch it off the AppKit main thread so a click never
    // freezes the Dock-owner window. The target host raises its own window from its own run loop.
    private static void FocusHostAsync(string hostName)
    {
        var thread = new Thread(() =>
        {
#pragma warning disable CA1031 // Focus is best-effort; a dead target must not crash the background thread.
            try
            {
                new CommandClient(hostName).Send("window.focus", paramsJson: null, timeoutMs: 2000);
            }
            catch
            {
                // Target gone / refused — nothing to do.
            }
#pragma warning restore CA1031
        })
        {
            IsBackground = true,
            Name = "radiant-dock-focus",
        };
        thread.Start();
    }
}
