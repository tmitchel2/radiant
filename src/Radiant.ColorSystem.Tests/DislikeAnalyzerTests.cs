using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.ColorSystem.Tests;

/// <summary>Upstream's dislike_analyzer_test.ts.</summary>
[TestClass]
public class DislikeAnalyzerTests
{
    private static readonly uint[] s_bileColors = [0xFF95884B, 0xFF716B40, 0xFFB08E00, 0xFF4C4308, 0xFF464521];

    [TestMethod]
    public void LikesMonkSkinToneScaleColors()
    {
        // From https://skintone.google#/get-started
        uint[] monkSkinToneScaleColors =
        [
            0xFFF6EDE4, 0xFFF3E7DB, 0xFFF7EAD0, 0xFFEADABA, 0xFFD7BD96,
            0xFFA07E56, 0xFF825C43, 0xFF604134, 0xFF3A312A, 0xFF292420,
        ];
        foreach (var color in monkSkinToneScaleColors)
        {
            Assert.IsFalse(DislikeAnalyzer.IsDisliked(Hct.FromInt(unchecked((int)color))), $"0x{color:X8}");
        }
    }

    [TestMethod]
    public void DislikesBileColors()
    {
        foreach (var color in s_bileColors)
        {
            Assert.IsTrue(DislikeAnalyzer.IsDisliked(Hct.FromInt(unchecked((int)color))), $"0x{color:X8}");
        }
    }

    [TestMethod]
    public void MakesBileColorsLikable()
    {
        foreach (var color in s_bileColors)
        {
            var hct = Hct.FromInt(unchecked((int)color));
            Assert.IsTrue(DislikeAnalyzer.IsDisliked(hct), $"0x{color:X8}");
            var likable = DislikeAnalyzer.FixIfDisliked(hct);
            Assert.IsFalse(DislikeAnalyzer.IsDisliked(likable), $"0x{color:X8} fixed");
        }
    }

    [TestMethod]
    public void LikesTone67Colors()
    {
        var color = Hct.From(100.0, 50.0, 67.0);

        Assert.IsFalse(DislikeAnalyzer.IsDisliked(color));
        Assert.AreEqual(color.ToInt(), DislikeAnalyzer.FixIfDisliked(color).ToInt());
    }
}
