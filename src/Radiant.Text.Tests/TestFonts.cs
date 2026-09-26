using System;
using System.IO;

namespace Radiant.Text.Tests;

/// <summary>Fonts for scripts Radiant's embedded fonts don't cover.</summary>
internal static class TestFonts
{
    private static readonly Lazy<FontFace> s_arabic = new(() => Load("NotoSansArabic.ttf", "Noto Sans Arabic"));
    private static readonly Lazy<FontFace> s_hebrew = new(() => Load("NotoSansHebrew.ttf", "Noto Sans Hebrew"));

    public static FontFace Arabic => s_arabic.Value;

    public static FontFace Hebrew => s_hebrew.Value;

    private static FontFace Load(string file, string family) =>
        FontFace.FromFile(Path.Combine(AppContext.BaseDirectory, "TestFonts", file), family);
}
