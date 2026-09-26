using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Text.Tests;

[TestClass]
public class FontTests
{
    private static FontFace Inter => FontLibrary.Default.FindFace(FontLibrary.Inter)!;

    [TestMethod]
    public void TheDefaultLibraryHasInterAndJetBrainsMonoWithItalics()
    {
        var library = FontLibrary.Default;

        CollectionAssert.AreEquivalent(new[] { FontLibrary.Inter, FontLibrary.JetBrainsMono }, library.Families.ToArray());
        Assert.IsTrue(library.FindFace(FontLibrary.Inter, italic: true)!.IsItalic);
        Assert.IsTrue(library.FindFace(FontLibrary.JetBrainsMono, italic: true)!.IsItalic);
    }

    [TestMethod]
    public void InterIsVariableInWeightAndOpticalSize()
    {
        var weight = Inter.FindAxis(FontVariation.Weight)!.Value;
        var opticalSize = Inter.FindAxis(FontVariation.OpticalSize)!.Value;

        Assert.AreEqual((100f, 400f, 900f), (weight.Min, weight.Default, weight.Max));
        Assert.AreEqual((14f, 32f), (opticalSize.Min, opticalSize.Max));
    }

    [TestMethod]
    public void AHeavierWeightSetsWiderText()
    {
        var regular = Inter.Instance(new FontVariation(FontVariation.Weight, FontWeight.Regular));
        var bold = Inter.Instance(new FontVariation(FontVariation.Weight, FontWeight.Bold));

        Assert.IsTrue(TextShaper.Shape("Hamburgefonstiv", bold, 16).Width > TextShaper.Shape("Hamburgefonstiv", regular, 16).Width);
    }

    [TestMethod]
    public void VariationsAreClampedToTheAxisAndInstancesAreShared()
    {
        var a = Inter.Instance(new FontVariation(FontVariation.Weight, 5000));
        var b = Inter.Instance(new FontVariation(FontVariation.Weight, 900));

        Assert.AreSame(a, b);
        Assert.AreEqual(900f, a.Variations.Single().Value);
    }

    [TestMethod]
    public void AxesTheFontLacksAreIgnored()
    {
        var mono = FontLibrary.Default.FindFace(FontLibrary.JetBrainsMono)!;

        var instance = mono.Instance(new FontVariation(FontVariation.OpticalSize, 20), new FontVariation(FontVariation.Weight, 500));

        Assert.AreEqual(FontVariation.Weight, instance.Variations.Single().Tag);
    }

    [TestMethod]
    public void ResolvingSetsOpticalSizeFromTheTextSize()
    {
        var small = FontLibrary.Default.Resolve(FontLibrary.Inter, FontWeight.Regular, italic: false, size: 12);
        var large = FontLibrary.Default.Resolve(FontLibrary.Inter, FontWeight.Regular, italic: false, size: 28);

        Assert.AreEqual(14f, small.Variations.Single(v => v.Tag == FontVariation.OpticalSize).Value, "clamped to the axis minimum");
        Assert.AreEqual(28f, large.Variations.Single(v => v.Tag == FontVariation.OpticalSize).Value);
    }

    [TestMethod]
    public void MetricsScaleWithSize()
    {
        var instance = Inter.Instance();

        var at16 = instance.Metrics(16);
        var at32 = instance.Metrics(32);

        Assert.AreEqual(at16.Ascender * 2, at32.Ascender, 1e-3f);
        // Inter's cap height is 0.727 em and its x-height 0.546 em.
        Assert.AreEqual(0.727f * 16, at16.CapHeight, 0.02f);
        Assert.AreEqual(0.546f * 16, at16.XHeight, 0.02f);
        Assert.IsTrue(at16.Descender > 0, "descent is a positive distance");
    }

    [TestMethod]
    public void OutlinesHaveTheirContours()
    {
        var instance = Inter.Instance();
        instance.TryGetGlyph('O', out var o);
        instance.TryGetGlyph('l', out var l);
        instance.TryGetGlyph(' ', out var space);

        Assert.AreEqual(2, instance.GetOutline(o).ContourCount, "O has an inside and an outside");
        Assert.AreEqual(1, instance.GetOutline(l).ContourCount);
        Assert.IsTrue(instance.GetOutline(space).IsEmpty);
    }

    [TestMethod]
    public void OutlinesFollowTheWeight()
    {
        var thin = Inter.Instance(new FontVariation(FontVariation.Weight, 100));
        var black = Inter.Instance(new FontVariation(FontVariation.Weight, 900));
        thin.TryGetGlyph('l', out var l);

        var thinWidth = thin.GetOutline(l).Bounds.Max.X - thin.GetOutline(l).Bounds.Min.X;
        var blackWidth = black.GetOutline(l).Bounds.Max.X - black.GetOutline(l).Bounds.Min.X;

        Assert.IsTrue(blackWidth > thinWidth * 2, $"stem {thinWidth} at 100, {blackWidth} at 900");
    }

    [TestMethod]
    public void MissingCharactersFallBackToAFaceThatHasThem()
    {
        var library = new FontLibrary();
        library.Register(FontLibrary.Default.FindFace(FontLibrary.Inter)!);
        library.Register(TestFonts.Arabic);

        Assert.AreSame(Inter, library.FaceFor('A', Inter));
        Assert.AreSame(TestFonts.Arabic, library.FaceFor('م', Inter));
        Assert.AreSame(Inter, library.FaceFor(0x1F600, Inter), "nothing has it: the preferred face draws its missing-glyph box");
    }

    [TestMethod]
    public void FontDataSurvivesACompactingCollection()
    {
        // HarfBuzz reads the font bytes in place; they must not move when the garbage collector compacts.
        var face = FontFace.FromBytes(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestFonts", "NotoSansHebrew.ttf")), "Probe");
        var before = TextShaper.Shape("abc", face.Instance(), 20).Advances.ToArray();
        for (var i = 0; i < 5; i++)
        {
            GC.KeepAlive(new byte[1 << 20]);
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        }
        var after = TextShaper.Shape("abc", face.Instance(), 20).Advances.ToArray();

        CollectionAssert.AreEqual(before, after);
    }
}
