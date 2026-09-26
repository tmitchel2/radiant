using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.ColorSystem.Tests;

/// <summary>Harmonizing and blending against upstream's outputs.</summary>
[TestClass]
public class BlendFixtureTests
{
    private static readonly double[] s_amounts = [0, 0.25, 0.5, 0.8, 1];

    [TestMethod]
    public void HarmonizedColoursMatchUpstream()
    {
        var mismatches = new Mismatches();
        foreach (var row in Fixture.Load("blend").EnumerateArray())
        {
            var (design, source) = (Fixture.Argb(row.GetProperty("design")), Fixture.Argb(row.GetProperty("source")));
            mismatches.Colour(
                Fixture.Argb(row.GetProperty("harmonize")),
                Blend.Harmonize(design, source),
                $"harmonize({Fixture.Hex(design)}, {Fixture.Hex(source)})");
        }
        mismatches.AssertNone();
    }

    [TestMethod]
    public void HueBlendsMatchUpstream()
    {
        var mismatches = new Mismatches();
        foreach (var row in Fixture.Load("blend").EnumerateArray())
        {
            var (design, source) = (Fixture.Argb(row.GetProperty("design")), Fixture.Argb(row.GetProperty("source")));
            var expected = row.GetProperty("hctHue");
            for (var i = 0; i < s_amounts.Length; i++)
            {
                mismatches.Colour(
                    Fixture.Argb(expected[i]),
                    Blend.HctHue(design, source, s_amounts[i]),
                    $"hctHue({Fixture.Hex(design)}, {Fixture.Hex(source)}, {s_amounts[i]})");
            }
        }
        mismatches.AssertNone();
    }

    [TestMethod]
    public void Cam16UcsBlendsMatchUpstream()
    {
        var mismatches = new Mismatches();
        foreach (var row in Fixture.Load("blend").EnumerateArray())
        {
            var (design, source) = (Fixture.Argb(row.GetProperty("design")), Fixture.Argb(row.GetProperty("source")));
            var expected = row.GetProperty("cam16Ucs");
            for (var i = 0; i < s_amounts.Length; i++)
            {
                mismatches.Colour(
                    Fixture.Argb(expected[i]),
                    Blend.Cam16Ucs(design, source, s_amounts[i]),
                    $"cam16Ucs({Fixture.Hex(design)}, {Fixture.Hex(source)}, {s_amounts[i]})");
            }
        }
        mismatches.AssertNone();
    }
}
