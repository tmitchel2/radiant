using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Platform.MacOS.Tests;

[TestClass]
public class AppearanceTests
{
    [TestMethod]
    public void DarkModeMatchesTheSystemSetting()
    {
        MacOnly.Require();
        // The `defaults` tool reads the same setting by another route: "Dark" when dark, and an
        // error (no such key) when light.
        using var process = Process.Start(new ProcessStartInfo("defaults", "read -g AppleInterfaceStyle")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        Assert.AreEqual(output == "Dark", MacAppearance.Read().IsDark);
    }

    [TestMethod]
    public void TheAccentColourIsAnOpaqueSrgbColour()
    {
        MacOnly.Require();
        var accent = MacAppearance.Read().AccentColor;

        Assert.AreEqual(0xFFu, accent >> 24, $"{accent:X8}");
        Assert.AreNotEqual(0xFF000000u, accent, "black: the colour wasn't read");
    }

    [TestMethod]
    public void TheSettingsAreStableBetweenReads()
    {
        MacOnly.Require();
        Assert.AreEqual(MacAppearance.Read(), MacAppearance.Read());
    }

    [TestMethod]
    public void TheAppearanceStartsWithTheCurrentSettings()
    {
        MacOnly.Require();
        using var appearance = new MacAppearance();
        var read = MacAppearance.Read();

        Assert.AreEqual(read.IsDark, appearance.IsDark);
        Assert.AreEqual(read.AccentColor, appearance.AccentColor);
        Assert.AreEqual(read.IncreaseContrast, appearance.IncreaseContrast);
        Assert.AreEqual(read.ReduceMotion, appearance.ReduceMotion);
    }

    [TestMethod]
    public void ASystemColourNotificationRaisesChangedWhenSomethingChanged()
    {
        MacOnly.Require();
        using var appearance = new MacAppearance();
        var changes = 0;
        appearance.Changed += () => changes++;
        var start = MacAppearance.Read();

        // Nothing changed: no event.
        Post("NSSystemColorsDidChangeNotification");
        Assert.AreEqual(0, changes);

        // The user switched to dark with more contrast: one event, and the new values.
        appearance.ReadOverride = () => start with { IsDark = !start.IsDark, IncreaseContrast = true };
        Post("NSSystemColorsDidChangeNotification");
        Assert.AreEqual(1, changes);
        Assert.AreEqual(!start.IsDark, appearance.IsDark);
        Assert.IsTrue(appearance.IncreaseContrast);

        // A second notification for the same change is ignored.
        Post("NSSystemColorsDidChangeNotification");
        Assert.AreEqual(1, changes);
    }

    [TestMethod]
    public void AfterDisposingNotificationsAreIgnored()
    {
        MacOnly.Require();
        var appearance = new MacAppearance();
        var changes = 0;
        appearance.Changed += () => changes++;
        appearance.ReadOverride = () => new AppearanceSnapshot(true, 0xFF123456, true, true);
        appearance.Dispose();

        Post("NSSystemColorsDidChangeNotification");

        Assert.AreEqual(0, changes);
    }

    // Default-centre notifications are delivered synchronously, on the posting thread.
    private static void Post(string constant)
    {
        using var pool = ObjC.Pool();
        var center = ObjC.Send(ObjC.Class("NSNotificationCenter"), "defaultCenter");
        ObjC.Send(center, "postNotificationName:object:", ObjC.AppKitConstant(constant), 0);
    }
}
