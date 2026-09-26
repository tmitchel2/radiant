using System;

namespace Radiant.Text.Tests;

/// <summary>A font library with Radiant's fonts and Noto Sans Hebrew and Arabic to fall back on.</summary>
internal static class TestLibrary
{
    private static readonly Lazy<FontLibrary> s_withFallbacks = new(() =>
    {
        // Shares the default library's faces and is never disposed, so neither disposes them.
        var library = new FontLibrary();
        library.Register(FontLibrary.Default.FindFace(FontLibrary.Inter)!);
        library.Register(FontLibrary.Default.FindFace(FontLibrary.Inter, italic: true)!);
        library.Register(TestFonts.Hebrew);
        library.Register(TestFonts.Arabic);
        return library;
    });

    public static FontLibrary WithFallbacks => s_withFallbacks.Value;
}
