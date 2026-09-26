using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.MaterialColor.Tests;

/// <summary>Colour temperature, complements and analogous colours against upstream's outputs.</summary>
[TestClass]
public class TemperatureFixtureTests
{
    [TestMethod]
    public void TemperaturesMatchUpstream()
    {
        var mismatches = new Mismatches();
        foreach (var row in Fixture.Load("temperature").EnumerateArray())
        {
            var argb = Fixture.Argb(row.GetProperty("argb"));
            var hct = Hct.FromInt(argb);
            var cache = new TemperatureCache(hct);
            var label = Fixture.Hex(argb);
            mismatches.Double(row.GetProperty("rawTemperature").GetDouble(), TemperatureCache.RawTemperature(hct), label + " raw temperature");
            mismatches.Double(row.GetProperty("inputRelativeTemperature").GetDouble(), cache.InputRelativeTemperature, label + " relative temperature");
        }
        mismatches.AssertNone();
    }

    [TestMethod]
    public void ComplementsWarmestAndColdestMatchUpstream()
    {
        var mismatches = new Mismatches();
        foreach (var row in Fixture.Load("temperature").EnumerateArray())
        {
            var argb = Fixture.Argb(row.GetProperty("argb"));
            var cache = new TemperatureCache(Hct.FromInt(argb));
            var label = Fixture.Hex(argb);
            mismatches.Colour(Fixture.Argb(row.GetProperty("complement")), cache.Complement.ToInt(), label + " complement");
            mismatches.Colour(Fixture.Argb(row.GetProperty("warmest")), cache.Warmest.ToInt(), label + " warmest");
            mismatches.Colour(Fixture.Argb(row.GetProperty("coldest")), cache.Coldest.ToInt(), label + " coldest");
        }
        mismatches.AssertNone();
    }

    [TestMethod]
    public void AnalogousColoursMatchUpstream()
    {
        var mismatches = new Mismatches();
        foreach (var row in Fixture.Load("temperature").EnumerateArray())
        {
            var argb = Fixture.Argb(row.GetProperty("argb"));
            // One cache for both calls, as the fixture was generated.
            var cache = new TemperatureCache(Hct.FromInt(argb));
            var label = Fixture.Hex(argb);
            CompareList(mismatches, row.GetProperty("analogous"), cache.Analogous(), label + " analogous");
            CompareList(mismatches, row.GetProperty("analogous3of6"), cache.Analogous(3, 6), label + " analogous(3, 6)");
        }
        mismatches.AssertNone();
    }

    private static void CompareList(Mismatches mismatches, JsonElement expected, IReadOnlyList<Hct> actual, string label)
    {
        Assert.AreEqual(expected.GetArrayLength(), actual.Count, label + " count");
        for (var i = 0; i < actual.Count; i++)
        {
            mismatches.Colour(Fixture.Argb(expected[i]), actual[i].ToInt(), $"{label}[{i}]");
        }
    }
}
