using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Platform.MacOS.Tests;

/// <summary>Test helpers for AppKit tests, which only mean anything on macOS.</summary>
internal static class MacOnly
{
    /// <summary>The category of tests that touch the user's real session (the clipboard, a window).</summary>
    public const string Integration = "Integration";

    /// <summary>
    /// Marks the test inconclusive off macOS. On macOS, makes sure there's an
    /// <c>NSApplication</c>, as in an app (GLFW makes one): without it AppKit's system cursors
    /// aren't loaded and come back nil.
    /// </summary>
    public static void Require()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Inconclusive("AppKit is only on macOS.");
        }
        MacPlatform.EnsureApplication();
    }
}
