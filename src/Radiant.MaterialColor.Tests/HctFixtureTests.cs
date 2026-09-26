using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.MaterialColor.Tests;

/// <summary>HCT, CAM16 and the HCT solver against upstream's outputs.</summary>
[TestClass]
public class HctFixtureTests
{
    [TestMethod]
    public void HueChromaAndToneMatchUpstreamAcrossTheRgbCube()
    {
        foreach (var row in Fixture.Load("hct").GetProperty("fromInt").EnumerateArray())
        {
            var argb = Fixture.Argb(row[0]);
            var hct = Hct.FromInt(argb);
            var label = Fixture.Hex(argb);
            Fixture.AreClose(row[1].GetDouble(), hct.Hue, label + " hue");
            Fixture.AreClose(row[2].GetDouble(), hct.Chroma, label + " chroma");
            Fixture.AreClose(row[3].GetDouble(), hct.Tone, label + " tone");
        }
    }

    [TestMethod]
    public void TheSolverFindsTheSameColoursAsUpstream()
    {
        var mismatches = 0;
        var total = 0;
        var first = "";
        foreach (var row in Fixture.Load("hct").GetProperty("solve").EnumerateArray())
        {
            var (hue, chroma, tone) = (row[0].GetDouble(), row[1].GetDouble(), row[2].GetDouble());
            var expected = Fixture.Argb(row[3]);
            var actual = HctSolver.SolveToInt(hue, chroma, tone);
            total++;
            if (actual != expected)
            {
                mismatches++;
                if (first.Length == 0)
                {
                    first = $"H{hue} C{chroma} T{tone}: expected {Fixture.Hex(expected)}, got {Fixture.Hex(actual)}";
                }
            }
        }
        Assert.AreEqual(0, mismatches, $"{mismatches} of {total} differ; first: {first}");
    }

    [TestMethod]
    public void Cam16CoordinatesMatchUpstream()
    {
        foreach (var row in Fixture.Load("hct").GetProperty("cam16").EnumerateArray())
        {
            var argb = Fixture.Argb(row[0]);
            var cam = Cam16.FromInt(argb);
            var label = Fixture.Hex(argb);
            double[] actual = [cam.Hue, cam.Chroma, cam.J, cam.Q, cam.M, cam.S, cam.JStar, cam.AStar, cam.BStar];
            for (var i = 0; i < actual.Length; i++)
            {
                Fixture.AreClose(row[i + 1].GetDouble(), actual[i], $"{label} component {i}");
            }
        }
    }

    [TestMethod]
    public void AColourSurvivesARoundTripThroughHct()
    {
        foreach (var row in Fixture.Load("hct").GetProperty("fromInt").EnumerateArray())
        {
            var argb = Fixture.Argb(row[0]);
            var hct = Hct.FromInt(argb);

            Assert.AreEqual(argb, Hct.From(hct.Hue, hct.Chroma, hct.Tone).ToInt(), Fixture.Hex(argb));
        }
    }

    [TestMethod]
    [DataRow(0.5, 1.0)]
    [DataRow(-0.5, -0.0)]
    [DataRow(2.5, 3.0)]
    [DataRow(-2.5, -2.0)]
    [DataRow(0.49999999999999994, 0.0)]
    [DataRow(1.4999999999999998, 1.0)]
    public void RoundingMatchesJavaScript(double value, double expected)
    {
        Assert.AreEqual(expected, MathUtils.Round(value));
    }
}
