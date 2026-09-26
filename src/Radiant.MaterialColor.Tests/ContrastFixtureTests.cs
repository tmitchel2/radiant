using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.MaterialColor.Tests;

/// <summary>Contrast ratios and the tones that reach them against upstream's outputs.</summary>
[TestClass]
public class ContrastFixtureTests
{
    [TestMethod]
    public void RatiosOfTonesMatchUpstream() => Compare("ratioOfTones", Contrast.RatioOfTones);

    [TestMethod]
    public void LighterTonesMatchUpstream() => Compare("lighter", Contrast.Lighter);

    [TestMethod]
    public void DarkerTonesMatchUpstream() => Compare("darker", Contrast.Darker);

    [TestMethod]
    public void UnsafeLighterTonesMatchUpstream() => Compare("lighterUnsafe", Contrast.LighterUnsafe);

    [TestMethod]
    public void UnsafeDarkerTonesMatchUpstream() => Compare("darkerUnsafe", Contrast.DarkerUnsafe);

    private static void Compare(string name, Func<double, double, double> function)
    {
        var mismatches = new Mismatches();
        foreach (var row in Fixture.Load("contrast").GetProperty(name).EnumerateArray())
        {
            var (a, b) = (row[0].GetDouble(), row[1].GetDouble());
            mismatches.Double(row[2].GetDouble(), function(a, b), $"{name}({a}, {b})");
        }
        mismatches.AssertNone();
    }
}
