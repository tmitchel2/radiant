using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Text.Tests;

[TestClass]
public class IconFontTests
{
    private static FontInstance Icons(float fill = 0) =>
        FontLibrary.Default.FindFace(FontLibrary.Icons)!.Instance(new FontVariation(FontVariation.Fill, fill));

    [TestMethod]
    public void AnIconsNameShapesToOneGlyph()
    {
        var run = TextShaper.Shape("check", Icons(), 24);

        Assert.AreEqual(1, run.Count);
        Assert.IsFalse(Icons().GetOutline(run.Glyphs[0]).IsEmpty);
        Assert.AreEqual(24f, run.Width, 0.5f, "icons are an em square");
    }

    [TestMethod]
    public void FillChangesTheOutline()
    {
        var outlined = Icons(0);
        var filled = Icons(1);
        var glyph = TextShaper.Shape("favorite", outlined, 24).Glyphs[0];

        Assert.AreNotEqual(outlined.GetOutline(glyph).ContourCount, filled.GetOutline(TextShaper.Shape("favorite", filled, 24).Glyphs[0]).ContourCount);
    }

    [TestMethod]
    public void IconsNeverStandInForMissingLetters()
    {
        var fallback = FontLibrary.Default.FaceFor(0x05D0, FontLibrary.Default.FindFace(FontLibrary.Inter)!);

        Assert.AreNotEqual(FontLibrary.Icons, fallback.FamilyName);
    }

    [TestMethod]
    public void AParagraphInTheIconFamilyDrawsTheIcon()
    {
        var paragraph = Paragraph.Layout("arrow_back", new TextStyle { FontFamily = FontLibrary.Icons, Size = 20 });

        Assert.AreEqual(1, paragraph.Lines[0].Runs.Sum(r => r.Shaped.Count));
    }
}
