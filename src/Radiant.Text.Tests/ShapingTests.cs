using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Text.Tests;

[TestClass]
public class ShapingTests
{
    private static FontInstance Inter => FontLibrary.Default.FindFace(FontLibrary.Inter)!.Instance();

    [TestMethod]
    public void KerningPullsAVTogether()
    {
        var kerned = TextShaper.Shape("AV", Inter, 20);
        var plain = TextShaper.Shape("AV", Inter, 20, new ShapeOptions { Features = [FontFeature.NoKerning] });

        Assert.IsTrue(kerned.Width < plain.Width - 1, $"{kerned.Width} vs {plain.Width}");
    }

    [TestMethod]
    public void TabularNumbersAreAllTheSameWidth()
    {
        var proportional = TextShaper.Shape("1118", Inter, 20);
        var tabular = TextShaper.Shape("1118", Inter, 20, new ShapeOptions { Features = [FontFeature.TabularNumbers] });

        Assert.AreNotEqual(proportional.Advances[0], proportional.Advances[3], "1 is narrower than 8 by default");
        Assert.AreEqual(1, tabular.Advances.Distinct().Count());
    }

    [TestMethod]
    public void SizeScalesEveryAdvance()
    {
        var at10 = TextShaper.Shape("Radiant", Inter, 10);
        var at20 = TextShaper.Shape("Radiant", Inter, 20);

        Assert.AreEqual(at10.Width * 2, at20.Width, 1e-3f);
    }

    [TestMethod]
    public void TrackingAddsSpaceAfterEveryGlyph()
    {
        var plain = TextShaper.Shape("abc", Inter, 20);
        var tracked = TextShaper.Shape("abc", Inter, 20, new ShapeOptions { Tracking = 0.1f });

        Assert.AreEqual(plain.Width + 3 * 2f, tracked.Width, 1e-3f);
    }

    [TestMethod]
    public void ClustersIndexIntoTheWholeText()
    {
        var run = TextShaper.Shape("xxHelloxx", 2, 5, Inter, 16);

        CollectionAssert.AreEqual(new[] { 2, 3, 4, 5, 6 }, run.Clusters.ToArray());
        Assert.AreEqual((2, 5), (run.Start, run.Length));
    }

    [TestMethod]
    public void ArabicLettersTakeTheirJoiningForms()
    {
        var arabic = TestFonts.Arabic.Instance();
        arabic.TryGetGlyph('م', out var isolatedMeem);

        // "مم": the first meem is initial and the second final; neither is the isolated form.
        var run = TextShaper.Shape("مم", arabic, 20, new ShapeOptions { Direction = TextDirection.RightToLeft });

        Assert.AreEqual(2, run.Count);
        Assert.IsTrue(run.Glyphs.All(g => g != isolatedMeem), "joined letters use joining forms");
    }

    [TestMethod]
    public void RightToLeftRunsComeOutInVisualOrder()
    {
        var hebrew = TestFonts.Hebrew.Instance();

        // "אב": alef first in memory, drawn rightmost.
        var run = TextShaper.Shape("אב", hebrew, 20, new ShapeOptions { Direction = TextDirection.RightToLeft });

        CollectionAssert.AreEqual(new[] { 1, 0 }, run.Clusters.ToArray(), "the leftmost glyph is the last character");
    }

    [TestMethod]
    public void ContextAcrossARunBoundaryKeepsArabicJoined()
    {
        var arabic = TestFonts.Arabic.Instance();
        var joinedAlone = TextShaper.Shape("مم", 1, 1, arabic, 20, new ShapeOptions { Direction = TextDirection.RightToLeft });
        var isolated = TextShaper.Shape("م", arabic, 20, new ShapeOptions { Direction = TextDirection.RightToLeft });

        // Shaping only the second meem, but with the first as context, gives its final form.
        Assert.AreNotEqual(isolated.Glyphs[0], joinedAlone.Glyphs[0]);
    }
}
