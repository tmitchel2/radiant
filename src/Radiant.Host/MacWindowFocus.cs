namespace Radiant.Host;

/// <summary>
/// Brings this process's app to the foreground on macOS via
/// <c>[NSApp activateIgnoringOtherApps:YES]</c>. Used alongside <see cref="Radiant.RadiantApplication.Focus"/>
/// when a host raises its own window in response to a cross-process <c>window.focus</c> command (the
/// Dock-menu "switch to this host's tab" action): <c>glfwFocusWindow</c> alone raises the window within
/// the app, while <c>activateIgnoringOtherApps:</c> guarantees the app itself comes to the front from a
/// background Dock-menu click (including across Spaces). Best-effort, exception-safe, no-op off macOS.
/// </summary>
internal static class MacWindowFocus
{
    public static void TryActivateApp()
    {
        if (!MacObjc.IsMac)
        {
            return;
        }
#pragma warning disable CA1031 // Bringing the app forward is cosmetic; never let it crash the host.
        try
        {
            var app = MacObjc.SharedApp();
            if (app == IntPtr.Zero)
            {
                return;
            }
            // activateIgnoringOtherApps: takes a BOOL — YES = 1 in the low byte.
            MacObjc.Send(app, MacObjc.Sel("activateIgnoringOtherApps:"), (IntPtr)1);
        }
        catch
        {
            // Best-effort foregrounding.
        }
#pragma warning restore CA1031
    }
}
