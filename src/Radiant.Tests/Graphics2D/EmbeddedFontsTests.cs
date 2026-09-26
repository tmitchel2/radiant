using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

[TestClass]
public class EmbeddedFontsTests
{
    [TestMethod]
    [DataRow(EmbeddedFonts.InterRegular, "Inter")]
    [DataRow(EmbeddedFonts.InterMedium, "Inter Medium")]
    [DataRow(EmbeddedFonts.InterSemiBold, "Inter SemiBold")]
    [DataRow(EmbeddedFonts.JetBrainsMonoRegular, "JetBrains Mono")]
    public void EachEmbeddedFontIsTheFaceItIsNamedFor(string name, string family)
    {
        using var font = MsdfFont.LoadEmbedded(name);

        Assert.AreEqual(family, font.FamilyName);
    }

    [TestMethod]
    public void TheDefaultFontIsInter()
    {
        using var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);

        Assert.AreEqual("Inter", font.FamilyName);
    }

    [TestMethod]
    public void TheNamesUsedBeforeTheFontsWereNamedStillLoad()
    {
        using var byOldDefault = MsdfFont.LoadEmbedded("default");
        using var byOldMonospace = MsdfFont.LoadEmbedded("monospace");

        Assert.AreEqual("Inter", byOldDefault.FamilyName);
        Assert.AreEqual("JetBrains Mono", byOldMonospace.FamilyName);
    }

    [TestMethod]
    [DataRow(EmbeddedFonts.Drafting)]
    [DataRow(EmbeddedFonts.DraftingMath)]
    [DataRow(EmbeddedFonts.DraftingShapes)]
    public void TheDraftingFallbacksStillLoad(string name)
    {
        using var font = MsdfFont.LoadEmbedded(name);

        Assert.IsFalse(string.IsNullOrEmpty(font.FamilyName));
    }

    [TestMethod]
    [DataRow(0x2300)] // ⌀ diameter: "S⌀ 5.00 mm"
    [DataRow(0x2312)] // ⌒ arc
    [DataRow(0x22A5)] // ⊥ perpendicularity
    [DataRow(0x2220)] // ∠ angularity
    [DataRow(0x2225)] // ∥ parallelism
    [DataRow(0x25CE)] // ◎ concentricity
    [DataRow(0x00D8)] // Ø
    [DataRow(0x00B1)] // ±
    public void TheDefaultFontDrawsEngineeringSymbols(int codepoint)
    {
        using var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);

        Assert.IsTrue(font.TryGetGlyph(codepoint, out var glyph), $"U+{codepoint:X4} has no entry");
        Assert.IsTrue(glyph.Width > 0 && glyph.Height > 0, $"U+{codepoint:X4} has no outline");
    }

    [TestMethod]
    public void TheDefaultFontIsKerned()
    {
        using var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);

        Assert.IsTrue(font.Kerning('A', 'V') < 0f, "AV should be pulled together");
        Assert.IsTrue(font.Kerning('T', 'o') < 0f, "To should be pulled together");
        Assert.AreEqual(0f, font.Kerning('H', 'H'));
    }

    [TestMethod]
    public void KerningNarrowsAMeasuredRun()
    {
        using var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);

        var pair = font.MeasureTextWidth("AV", 40f);
        var apart = font.MeasureTextWidth("A", 40f) + font.MeasureTextWidth("V", 40f);

        Assert.IsTrue(pair < apart - 1f, $"AV measures {pair}px against {apart}px unkerned");
    }

    [TestMethod]
    public void TheMonospaceFontHasOneAdvance()
    {
        using var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Monospace);

        Assert.AreEqual(font.MeasureTextWidth("iiii", 20f), font.MeasureTextWidth("MMMM", 20f), 1e-3f);
    }

    [TestMethod]
    [DataRow('H')]
    [DataRow('x')]
    [DataRow(0x22A5)] // ⊥, baked from a Noto fallback with different vertical metrics
    [DataRow(0x2220)] // ∠, likewise
    public void FlatBottomedGlyphsSitOnTheBaseline(int codepoint)
    {
        using var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);
        Assert.IsTrue(font.TryGetGlyph(codepoint, out var glyph));

        // The quad extends past the outline by the distance-range padding on every side.
        var paddingEm = MathF.Ceiling(font.DistanceRangePx) / font.GlyphPixelSize;
        var outlineBottomEm = glyph.BearingY + glyph.Height - paddingEm;

        // Within a pixel at the bake size: fallback glyphs once floated a quarter of an em high.
        Assert.AreEqual(0f, outlineBottomEm, 1f / font.GlyphPixelSize, $"U+{codepoint:X4} bottom is {outlineBottomEm}em from the baseline");
    }

    [TestMethod]
    public void CapitalsRiseToInterCapHeight()
    {
        using var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);
        Assert.IsTrue(font.TryGetGlyph('H', out var glyph));

        var paddingEm = MathF.Ceiling(font.DistanceRangePx) / font.GlyphPixelSize;
        var topAboveBaselineEm = -(glyph.BearingY + paddingEm);

        // Inter's cap height is 0.727em.
        Assert.AreEqual(0.727f, topAboveBaselineEm, 1f / font.GlyphPixelSize);
    }
}
