namespace Radiant.Host;

/// <summary>
/// Runs a native macOS <c>NSOpenPanel</c> so the File ▸ Open… menu item can pick a document or folder.
/// Defaults its starting directory to the filesystem root <c>/</c> and allows choosing either a file with
/// one of <see cref="RadiantAppIdentity.OpenFileExtensions"/> or any directory. No-op (returns <c>null</c>)
/// off macOS.
///
/// <para>Must be called on the AppKit main thread — i.e. only from a menu-action callback (which AppKit
/// dispatches on main), never a background thread. <c>runModal</c> blocks the run loop while the dialog is
/// open, which is the standard, expected behaviour for a modal File→Open panel.</para>
/// </summary>
internal static class MacOpenPanel
{
    // NSModalResponseOK — the user clicked "Open".
    private const long NSModalResponseOK = 1;

    /// <summary>
    /// Shows the open panel and returns the chosen absolute path, or <c>null</c> if the user cancelled,
    /// the platform is not macOS, or anything in the native bridge fails.
    /// </summary>
    public static string? Run()
    {
        if (!MacObjc.IsMac)
        {
            return null;
        }
#pragma warning disable CA1031 // The panel is best-effort UI; a native fault must never crash the host.
        try
        {
            var panel = MacObjc.Send(MacObjc.Cls("NSOpenPanel"), MacObjc.Sel("openPanel"));
            if (panel == IntPtr.Zero)
            {
                return null;
            }

            MacObjc.Send(panel, MacObjc.Sel("setCanChooseFiles:"), true);
            MacObjc.Send(panel, MacObjc.Sel("setCanChooseDirectories:"), true);
            MacObjc.Send(panel, MacObjc.Sel("setAllowsMultipleSelection:"), false);

            // Filter files to the application's extensions (directories remain selectable regardless of
            // this list); with none, any file may be chosen.
            var extensions = RadiantAppIdentity.Current.OpenFileExtensions;
            if (extensions.Count > 0)
            {
                var types = MacObjc.Send(MacObjc.Cls("NSMutableArray"), MacObjc.Sel("array"));
                foreach (var extension in extensions)
                {
                    MacObjc.Send(types, MacObjc.Sel("addObject:"), MacObjc.NSString(extension));
                }
                MacObjc.Send(panel, MacObjc.Sel("setAllowedFileTypes:"), types);
            }

            // Start at the filesystem root, matching the in-window browser's default root.
            var url = MacObjc.Send(MacObjc.Cls("NSURL"), MacObjc.Sel("fileURLWithPath:"), MacObjc.NSString("/"));
            MacObjc.Send(panel, MacObjc.Sel("setDirectoryURL:"), url);

            var result = MacObjc.Send(panel, MacObjc.Sel("runModal"));
            if ((long)result != NSModalResponseOK)
            {
                return null;
            }

            var urls = MacObjc.Send(panel, MacObjc.Sel("URLs"));
            if (urls == IntPtr.Zero)
            {
                return null;
            }
            var first = MacObjc.Send(urls, MacObjc.Sel("objectAtIndex:"), IntPtr.Zero); // index 0
            if (first == IntPtr.Zero)
            {
                return null;
            }
            var nsPath = MacObjc.Send(first, MacObjc.Sel("path"));
            var path = MacObjc.ReadUtf8(MacObjc.Send(nsPath, MacObjc.Sel("UTF8String")));
            return string.IsNullOrEmpty(path) ? null : path;
        }
        catch
        {
            return null;
        }
#pragma warning restore CA1031
    }
}
