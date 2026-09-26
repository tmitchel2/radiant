using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Radiant.Platform.MacOS;

/// <summary>
/// The user's appearance settings from AppKit, kept current by observing their changes.
/// <list type="bullet">
/// <item><b>Dark mode:</b> <c>NSApp.effectiveAppearance</c> matched against Aqua and Dark Aqua,
/// observed by key-value observing. Without an <c>NSApplication</c> (tests, tools) it falls back
/// to the <c>AppleInterfaceStyle</c> default.</item>
/// <item><b>Accent:</b> <c>NSColor.controlAccentColor</c> converted to sRGB, re-read on
/// <c>NSSystemColorsDidChangeNotification</c>.</item>
/// <item><b>Contrast and motion:</b> <c>NSWorkspace</c>'s
/// <c>accessibilityDisplayShouldIncreaseContrast</c> and <c>…ShouldReduceMotion</c>, re-read on
/// <c>NSWorkspaceAccessibilityDisplayOptionsDidChangeNotification</c>.</item>
/// </list>
/// The distributed <c>AppleInterfaceThemeChangedNotification</c> is observed too, as a second
/// route for the dark-mode switch. Every notification re-reads all four settings and raises
/// <see cref="Changed"/> only if something differs, so duplicates cost nothing.
/// <para>
/// AppKit delivers these on the main thread's run loop, which GLFW pumps when it polls for
/// events, so <see cref="Changed"/> arrives on the UI thread between frames.
/// </para>
/// </summary>
internal sealed unsafe class MacAppearance : IAppearance, IDisposable
{
    private const string ObserverClassName = "RadiantAppearanceObserver";
    private const string NotificationSelector = "radiantAppearanceChanged:";
    private const string AppearanceKeyPath = "effectiveAppearance";

    // Native callbacks can't carry managed state, so each observer object maps to its owner.
    private static readonly Dictionary<nint, MacAppearance> s_owners = [];

    private nint _observer;
    private nint _observedApp;
    private AppearanceSnapshot _current;

    /// <summary>Reads the settings and starts observing them.</summary>
    public MacAppearance()
    {
        _current = Read();
        Observe();
    }

    /// <inheritdoc/>
    public event Action? Changed;

    /// <inheritdoc/>
    public bool IsDark => _current.IsDark;

    /// <inheritdoc/>
    public uint AccentColor => _current.AccentColor;

    /// <inheritdoc/>
    public bool IncreaseContrast => _current.IncreaseContrast;

    /// <inheritdoc/>
    public bool ReduceMotion => _current.ReduceMotion;

    /// <summary>Replaces reading AppKit, so tests can check a notification leads to <see cref="Changed"/>.</summary>
    internal Func<AppearanceSnapshot>? ReadOverride { get; set; }

    /// <summary>Re-reads the settings, raising <see cref="Changed"/> if any differ.</summary>
    public void Refresh()
    {
        var next = ReadOverride?.Invoke() ?? Read();
        if (next != _current)
        {
            _current = next;
            Changed?.Invoke();
        }
    }

    /// <summary>The settings as AppKit has them now.</summary>
    public static AppearanceSnapshot Read()
    {
        using var pool = ObjC.Pool();
        var workspace = ObjC.Send(ObjC.Class("NSWorkspace"), "sharedWorkspace");
        return new AppearanceSnapshot(
            ReadIsDark(),
            ReadAccentColor(),
            ObjC.GetBool(workspace, "accessibilityDisplayShouldIncreaseContrast"),
            ObjC.GetBool(workspace, "accessibilityDisplayShouldReduceMotion"));
    }

    private static bool ReadIsDark()
    {
        var app = ObjC.AppKitConstant("NSApp");
        if (app == 0)
        {
            // No NSApplication: don't create one (off the main thread that asserts); the user
            // default says "Dark" whenever the system is dark, including under Auto.
            var defaults = ObjC.Send(ObjC.Class("NSUserDefaults"), "standardUserDefaults");
            var style = ObjC.ToManagedString(ObjC.Send(defaults, "stringForKey:", ObjC.String("AppleInterfaceStyle")));
            return string.Equals(style, "Dark", StringComparison.OrdinalIgnoreCase);
        }
        var appearance = ObjC.Send(app, "effectiveAppearance");
        var dark = ObjC.AppKitConstant("NSAppearanceNameDarkAqua");
        var names = ObjC.NSArray(ObjC.AppKitConstant("NSAppearanceNameAqua"), dark);
        var best = ObjC.Send(appearance, "bestMatchFromAppearancesWithNames:", names);
        return best != 0 && ObjC.GetBool(best, "isEqualToString:", dark);
    }

    private static uint ReadAccentColor()
    {
        var accent = ObjC.Send(ObjC.Class("NSColor"), "controlAccentColor");
        var srgb = ObjC.Send(accent, "colorUsingColorSpace:", ObjC.Send(ObjC.Class("NSColorSpace"), "sRGBColorSpace"));
        if (srgb == 0)
        {
            return 0xFF007AFF; // macOS's default blue, should the conversion ever fail
        }
        double r, g, b, a;
        ((delegate* unmanaged<nint, nint, double*, double*, double*, double*, void>)ObjC.MsgSend)(
            srgb, ObjC.Sel("getRed:green:blue:alpha:"), &r, &g, &b, &a);
        return (Channel(a) << 24) | (Channel(r) << 16) | (Channel(g) << 8) | Channel(b);

        static uint Channel(double value) => (uint)Math.Round(Math.Clamp(value, 0, 1) * 255);
    }

    private void Observe()
    {
        using var pool = ObjC.Pool();
        var cls = ObjC.DefineClass(ObserverClassName,
            ("observeValueForKeyPath:ofObject:change:context:",
                (nint)(delegate* unmanaged<nint, nint, nint, nint, nint, nint, void>)&OnKeyValueChanged, "v@:@@@^v"),
            (NotificationSelector, (nint)(delegate* unmanaged<nint, nint, nint, void>)&OnNotification, "v@:@"));
        _observer = ObjC.Send(ObjC.Send(cls, "alloc"), "init");
        s_owners[_observer] = this;

        var selector = ObjC.Sel(NotificationSelector);
        var workspaceCenter = ObjC.Send(ObjC.Send(ObjC.Class("NSWorkspace"), "sharedWorkspace"), "notificationCenter");
        ObjC.Send(workspaceCenter, "addObserver:selector:name:object:", _observer, selector,
            ObjC.AppKitConstant("NSWorkspaceAccessibilityDisplayOptionsDidChangeNotification"), 0);
        ObjC.Send(DefaultCenter, "addObserver:selector:name:object:", _observer, selector,
            ObjC.AppKitConstant("NSSystemColorsDidChangeNotification"), 0);
        ObjC.Send(DistributedCenter, "addObserver:selector:name:object:", _observer, selector,
            ObjC.String("AppleInterfaceThemeChangedNotification"), 0);

        _observedApp = ObjC.AppKitConstant("NSApp");
        if (_observedApp != 0)
        {
            ObjC.Send(_observedApp, "addObserver:forKeyPath:options:context:", _observer, ObjC.String(AppearanceKeyPath), 0, 0);
        }
    }

    private static nint DefaultCenter => ObjC.Send(ObjC.Class("NSNotificationCenter"), "defaultCenter");

    private static nint DistributedCenter => ObjC.Send(ObjC.Class("NSDistributedNotificationCenter"), "defaultCenter");

    [UnmanagedCallersOnly]
    private static void OnKeyValueChanged(nint self, nint cmd, nint keyPath, nint obj, nint change, nint context) => Notify(self);

    [UnmanagedCallersOnly]
    private static void OnNotification(nint self, nint cmd, nint notification) => Notify(self);

    private static void Notify(nint observer)
    {
#pragma warning disable CA1031 // An exception must not unwind into AppKit, which would abort the process.
        try
        {
            if (s_owners.TryGetValue(observer, out var owner))
            {
                owner.Refresh();
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[platform] appearance change handler failed: {e}");
        }
#pragma warning restore CA1031
    }

    /// <summary>Stops observing.</summary>
    public void Dispose()
    {
        if (_observer == 0)
        {
            return;
        }
        using var pool = ObjC.Pool();
        if (_observedApp != 0)
        {
            ObjC.Send(_observedApp, "removeObserver:forKeyPath:", _observer, ObjC.String(AppearanceKeyPath));
        }
        ObjC.Send(ObjC.Send(ObjC.Send(ObjC.Class("NSWorkspace"), "sharedWorkspace"), "notificationCenter"), "removeObserver:", _observer);
        ObjC.Send(DefaultCenter, "removeObserver:", _observer);
        ObjC.Send(DistributedCenter, "removeObserver:", _observer);
        s_owners.Remove(_observer);
        ObjC.Send(_observer, "release");
        _observer = 0;
    }
}
