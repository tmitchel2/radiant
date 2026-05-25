using Radiant.Graphics2D;

namespace Radiant.Tests;

/// <summary>
/// Loads the embedded default MSDF font once per test-assembly load. Used by
/// UI-primitive tests that construct <see cref="Radiant.UI.Button"/>,
/// <see cref="Radiant.UI.Label"/> etc. — those primitives now require an
/// <see cref="MsdfFont"/> at construction time.
/// </summary>
internal static class TestFonts
{
    public static readonly MsdfFont Default = MsdfFont.LoadEmbedded("default");
}
