using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.ColorSystem.Tests;

/// <summary>Upstream's palettes_test.ts (TonalPalette and KeyColor; CorePalette is not ported).</summary>
[TestClass]
public class TonalPaletteTests
{
    [TestMethod]
    [DataRow(100.0, 0xFFFFFFFFu)]
    [DataRow(95.0, 0xFFF1EFFFu)]
    [DataRow(90.0, 0xFFE0E0FFu)]
    [DataRow(80.0, 0xFFBEC2FFu)]
    [DataRow(70.0, 0xFF9DA3FFu)]
    [DataRow(60.0, 0xFF7C84FFu)]
    [DataRow(50.0, 0xFF5A64FFu)]
    [DataRow(40.0, 0xFF343DFFu)]
    [DataRow(30.0, 0xFF0000EFu)]
    [DataRow(20.0, 0xFF0001ACu)]
    [DataRow(10.0, 0xFF00006Eu)]
    [DataRow(0.0, 0xFF000000u)]
    public void TonesOfBlueMatchUpstream(double tone, uint expected)
    {
        var blue = TonalPalette.FromInt(unchecked((int)0xFF0000FFu));

        Assert.AreEqual(unchecked((int)expected), blue.Tone(tone));
    }

    [TestMethod]
    public void KeyColorWithExactChromaHasThatChroma()
    {
        // Requested chroma is exactly achievable at a certain tone.
        var palette = TonalPalette.FromHueAndChroma(50.0, 60.0);
        var result = palette.KeyColor;

        Assert.IsTrue(Math.Abs(result.Hue - 50.0) < 10.0, $"hue {result.Hue}");
        Assert.IsTrue(Math.Abs(result.Chroma - 60.0) < 0.5, $"chroma {result.Chroma}");
        // Tone might vary, but should be within the range from 0 to 100.
        Assert.IsTrue(result.Tone > 0 && result.Tone < 100, $"tone {result.Tone}");
    }

    [TestMethod]
    public void KeyColorWithUnusuallyHighChromaReachesTheChromaPeak()
    {
        // Requested chroma is above what is achievable. For Hue 149, chroma peak
        // is 89.6 at Tone 87.9. The result key color's chroma should be close to
        // the chroma peak.
        var palette = TonalPalette.FromHueAndChroma(149.0, 200.0);
        var result = palette.KeyColor;

        Assert.IsTrue(Math.Abs(result.Hue - 149.0) < 10.0, $"hue {result.Hue}");
        Assert.IsTrue(result.Chroma > 89.0, $"chroma {result.Chroma}");
        // Tone might vary, but should be within the range from 0 to 100.
        Assert.IsTrue(result.Tone > 0 && result.Tone < 100, $"tone {result.Tone}");
    }

    [TestMethod]
    public void KeyColorWithUnusuallyLowChromaIsNearTone50()
    {
        // By definition, the key color should be the first tone, starting from
        // Tone 50, matching the given hue and chroma. When requesting a very low
        // chroma, the result should be close to Tone 50, since most tones can
        // produce a low chroma.
        var palette = TonalPalette.FromHueAndChroma(50.0, 3.0);
        var result = palette.KeyColor;

        Assert.IsTrue(Math.Abs(result.Hue - 50.0) < 10.0, $"hue {result.Hue}");
        Assert.IsTrue(Math.Abs(result.Chroma - 3.0) < 0.5, $"chroma {result.Chroma}");
        Assert.IsTrue(Math.Abs(result.Tone - 50.0) < 0.5, $"tone {result.Tone}");
    }

    [TestMethod]
    public void FromHctKeepsTheColourAsItsKeyColor()
    {
        var hct = Hct.FromInt(unchecked((int)0xFF6750A4u));

        var palette = TonalPalette.FromHct(hct);

        Assert.AreSame(hct, palette.KeyColor);
        Assert.AreEqual(hct.Hue, palette.Hue);
        Assert.AreEqual(hct.Chroma, palette.Chroma);
    }

    [TestMethod]
    public void GetHctIsTheToneAsHct()
    {
        var palette = TonalPalette.FromInt(unchecked((int)0xFF6750A4u));

        Assert.AreEqual(palette.Tone(40), palette.GetHct(40).ToInt());
    }

    [TestMethod]
    public void TonesComputedConcurrentlyMatchTonesComputedAlone()
    {
        // Yellow, so tone 99 averages tones 98 and 100 through the cache as well.
        var expected = TonalPalette.FromInt(unchecked((int)0xFFFBBC05u));
        var shared = TonalPalette.FromInt(unchecked((int)0xFFFBBC05u));
        var tones = Enumerable.Range(0, 101).Select(t => (double)t).ToArray();

        Parallel.For(0, 64, i =>
        {
            foreach (var tone in tones.Reverse().Skip(i % 7))
            {
                _ = shared.Tone(tone);
            }
        });

        foreach (var tone in tones)
        {
            Assert.AreEqual(expected.Tone(tone), shared.Tone(tone), $"T{tone}");
        }
    }
}
