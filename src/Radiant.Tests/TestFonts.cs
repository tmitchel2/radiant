using Radiant.Graphics2D;

namespace Radiant.Tests;

/// <summary>
/// Loads the embedded default MSDF font once per test-assembly load, for the rendering tests that
/// draw MSDF text.
/// </summary>
internal static class TestFonts
{
    public static readonly MsdfFont Default = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);
}
