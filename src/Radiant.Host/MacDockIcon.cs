using System.Runtime.InteropServices;

namespace Radiant.Host;

/// <summary>
/// Sets the macOS Dock icon at runtime from the application's icon
/// (<see cref="RadiantAppIdentity.DockIcon"/>). A plain <c>dotnet run</c> launch and the NativeAOT
/// published binary are both bare executables (not <c>.app</c> bundles), so macOS
/// otherwise shows the generic executable icon in the Dock. GLFW does not set the
/// Dock icon on macOS (it only honours window icons on X11/Win32), so we drive it
/// directly through <c>NSApplication.setApplicationIconImage:</c> via the Objective-C
/// runtime — the same mechanism a bundled app's <c>CFBundleIconFile</c> uses, applied
/// programmatically. Call once after the GUI app has been initialised (i.e. after
/// GLFW has created the NSApplication). No-op (and never throws) off macOS.
/// </summary>
public static unsafe class MacDockIcon
{
    private static bool s_applied;
    private static bool s_accessoryApplied;

    /// <summary>
    /// Best-effort: make the installed identity's icon the Dock icon for this process.
    /// Idempotent and exception-safe — a failure to brand the Dock must never take down
    /// the app, so any error is swallowed.
    /// </summary>
    public static void TrySet()
    {
        if (s_applied || !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;
        s_applied = true;

#pragma warning disable CA1031 // Branding the Dock is cosmetic; never let it crash the app.
        try
        {
            var png = RadiantAppIdentity.Current.DockIcon();
            if (png is null || png.Length == 0)
                return;

            var app = objc_msgSend(ObjcClass("NSApplication"), Sel("sharedApplication"));
            if (app == IntPtr.Zero)
                return;

            IntPtr data;
            fixed (byte* p = png)
            {
                data = objc_msgSend_bytes_len(
                    ObjcClass("NSData"), Sel("dataWithBytes:length:"), (IntPtr)p, (UIntPtr)png.Length);
            }
            if (data == IntPtr.Zero)
                return;

            var image = objc_msgSend(ObjcClass("NSImage"), Sel("alloc"));
            image = objc_msgSend_ptr(image, Sel("initWithData:"), data);
            if (image == IntPtr.Zero)
                return;

            objc_msgSend_ptr(app, Sel("setApplicationIconImage:"), image);
        }
        catch
        {
            // Cosmetic only — never propagate.
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Make this process a macOS "accessory" app: present, but with no Dock tile and no menu-bar
    /// presence — the programmatic equivalent of an Info.plist <c>LSUIElement</c>. Used by auxiliary
    /// windows (e.g. the transparent click-through drag overlay) that open a GLFW/NSApplication window
    /// — and so would otherwise get a generic Dock icon — but should never appear in the Dock.
    ///
    /// <para>GLFW sets the activation policy to <c>Regular</c> while creating the window, so this must be
    /// called from a run-loop callback (e.g. the first frame), after the window exists, to win the race
    /// and switch to <c>Accessory</c>. macOS honours an activation-policy change at runtime. Idempotent
    /// and exception-safe; no-op (and never throws) off macOS.</para>
    /// </summary>
    public static void TrySetAccessoryPolicy()
    {
        if (s_accessoryApplied || !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;
        s_accessoryApplied = true;

#pragma warning disable CA1031 // Hiding the Dock tile is cosmetic; never let it crash the app.
        try
        {
            var app = objc_msgSend(ObjcClass("NSApplication"), Sel("sharedApplication"));
            if (app == IntPtr.Zero)
                return;

            // NSApplicationActivationPolicyAccessory = 1 (no Dock tile, no menu bar).
            objc_msgSend_nint(app, Sel("setActivationPolicy:"), (IntPtr)1);
        }
        catch
        {
            // Cosmetic only — never propagate.
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Restore this process to a macOS "regular" app (Dock tile + menu-bar presence) — the inverse of
    /// <see cref="TrySetAccessoryPolicy"/>. Used when a former accessory host is re-elected as the Dock
    /// owner (the previous owner died) and must show the single Dock tile again. After this, a fresh
    /// <see cref="TrySet"/> brands the now-visible tile. Idempotent and exception-safe; no-op off macOS.
    /// </summary>
    public static void TrySetRegularPolicy()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

#pragma warning disable CA1031 // Showing the Dock tile is cosmetic; never let it crash the app.
        try
        {
            var app = objc_msgSend(ObjcClass("NSApplication"), Sel("sharedApplication"));
            if (app == IntPtr.Zero)
                return;

            // NSApplicationActivationPolicyRegular = 0 (Dock tile + menu bar).
            objc_msgSend_nint(app, Sel("setActivationPolicy:"), (IntPtr)0);
            // Allow a subsequent re-brand now that the tile is visible again.
            s_accessoryApplied = false;
        }
        catch
        {
            // Cosmetic only — never propagate.
        }
#pragma warning restore CA1031
    }

    /// <summary>Radiant's own icon, the default <see cref="RadiantAppIdentity.DockIcon"/>.</summary>
    public static byte[]? LoadRadiantIcon() =>
        LoadEmbeddedPng(typeof(MacDockIcon).Assembly, "Radiant.Host.Resources.icon.png");

    /// <summary>
    /// The bytes of an embedded PNG, for an application's <see cref="RadiantAppIdentity.DockIcon"/>; null
    /// when <paramref name="assembly"/> has no resource of that name. The logical name of an
    /// <c>EmbeddedResource</c> is <c>&lt;RootNamespace&gt;.&lt;dir&gt;.&lt;file&gt;</c>.
    /// </summary>
    public static byte[]? LoadEmbeddedPng(System.Reflection.Assembly assembly, string resourceName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    // --- Objective-C runtime interop (UTF8 byte* args to satisfy CA2101, no string marshalling) ---

    private static IntPtr ObjcClass(string name)
    {
        var utf8 = System.Text.Encoding.UTF8.GetBytes(name + "\0");
        fixed (byte* p = utf8) return objc_getClass((IntPtr)p);
    }

    private static IntPtr Sel(string name)
    {
        var utf8 = System.Text.Encoding.UTF8.GetBytes(name + "\0");
        fixed (byte* p = utf8) return sel_registerName((IntPtr)p);
    }

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr objc_getClass(IntPtr name);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr sel_registerName(IntPtr name);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ptr(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_bytes_len(
        IntPtr receiver, IntPtr selector, IntPtr bytes, UIntPtr length);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_nint(IntPtr receiver, IntPtr selector, IntPtr arg);
}
