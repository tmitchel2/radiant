using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.ColorSystem.Tests;

/// <summary>Upstream's temperature_cache_test.ts, and how the port's cache behaves.</summary>
[TestClass]
public class TemperatureCacheTests
{
    // Jasmine's toBeCloseTo(expected, 3).
    private const double Delta = 0.0005;

    [TestMethod]
    [DataRow(0xFF0000FFu, -1.393)]
    [DataRow(0xFFFF0000u, 2.351)]
    [DataRow(0xFF00FF00u, -0.267)]
    [DataRow(0xFFFFFFFFu, -0.5)]
    [DataRow(0xFF000000u, -0.5)]
    public void ComputesRawTemperatures(uint argb, double expected)
    {
        Assert.AreEqual(expected, TemperatureCache.RawTemperature(Hct.FromInt(unchecked((int)argb))), Delta);
    }

    [TestMethod]
    [DataRow(0xFF0000FFu, 0.0)]
    [DataRow(0xFFFF0000u, 1.0)]
    [DataRow(0xFF00FF00u, 0.467)]
    [DataRow(0xFFFFFFFFu, 0.5)]
    [DataRow(0xFF000000u, 0.5)]
    public void ComputesRelativeTemperatures(uint argb, double expected)
    {
        Assert.AreEqual(expected, new TemperatureCache(Hct.FromInt(unchecked((int)argb))).InputRelativeTemperature, Delta);
    }

    [TestMethod]
    [DataRow(0xFF0000FFu, 0xFF9D0002u)]
    [DataRow(0xFFFF0000u, 0xFF007BFCu)]
    [DataRow(0xFF00FF00u, 0xFFFFD2C9u)]
    [DataRow(0xFFFFFFFFu, 0xFFFFFFFFu)]
    [DataRow(0xFF000000u, 0xFF000000u)]
    public void FindsComplements(uint argb, uint expected)
    {
        var complement = new TemperatureCache(Hct.FromInt(unchecked((int)argb))).Complement.ToInt();

        Assert.AreEqual(Fixture.Hex(unchecked((int)expected)), Fixture.Hex(complement));
    }

    [TestMethod]
    [DataRow(0xFF0000FFu, new[] { 0xFF00590Cu, 0xFF00564Eu, 0xFF0000FFu, 0xFF6700CCu, 0xFF81009Fu })]
    [DataRow(0xFFFF0000u, new[] { 0xFFF60082u, 0xFFFC004Cu, 0xFFFF0000u, 0xFFD95500u, 0xFFAF7200u })]
    [DataRow(0xFF00FF00u, new[] { 0xFFCEE900u, 0xFF92F500u, 0xFF00FF00u, 0xFF00FD6Fu, 0xFF00FAB3u })]
    [DataRow(0xFF000000u, new[] { 0xFF000000u, 0xFF000000u, 0xFF000000u, 0xFF000000u, 0xFF000000u })]
    [DataRow(0xFFFFFFFFu, new[] { 0xFFFFFFFFu, 0xFFFFFFFFu, 0xFFFFFFFFu, 0xFFFFFFFFu, 0xFFFFFFFFu })]
    public void FindsAnalogousColours(uint argb, uint[] expected)
    {
        var analogous = new TemperatureCache(Hct.FromInt(unchecked((int)argb))).Analogous();

        CollectionAssert.AreEqual(
            expected.Select(e => Fixture.Hex(unchecked((int)e))).ToArray(),
            analogous.Select(h => Fixture.Hex(h.ToInt())).ToArray());
    }

    [TestMethod]
    public void AnalogousColoursIncludeTheInputItself()
    {
        var input = Hct.FromInt(unchecked((int)0xFF6750A4u));

        var analogous = new TemperatureCache(input).Analogous();

        Assert.AreSame(input, analogous[2]);
    }

    [TestMethod]
    public void HctsByTempHoldEveryHueAndTheInputColdestFirst()
    {
        var input = Hct.FromInt(unchecked((int)0xFF6750A4u));
        var cache = new TemperatureCache(input);

        Assert.AreEqual(361, cache.HctsByHue.Count);
        Assert.AreEqual(362, cache.HctsByTemp.Count);
        Assert.AreEqual(362, cache.TempsByHct.Count);
        Assert.IsTrue(cache.HctsByTemp.Contains(input));
        Assert.AreSame(cache.HctsByTemp[0], cache.Coldest);
        Assert.AreSame(cache.HctsByTemp[^1], cache.Warmest);
        for (var i = 1; i < cache.HctsByTemp.Count; i++)
        {
            Assert.IsTrue(cache.TempsByHct[cache.HctsByTemp[i - 1]] <= cache.TempsByHct[cache.HctsByTemp[i]]);
        }
    }

    [TestMethod]
    public void EqualTemperaturesKeepTheirOrderInHue()
    {
        // At T100 every hue is white, so every temperature ties: a stable sort leaves hue order, then
        // the input, as JavaScript's does.
        var input = Hct.FromInt(unchecked((int)0xFFFFFFFFu));
        var cache = new TemperatureCache(input);

        CollectionAssert.AreEqual(cache.HctsByHue.Append(input).ToArray(), cache.HctsByTemp.ToArray());
    }

    [TestMethod]
    public void RelativeTemperatureRefusesAColourFromElsewhere()
    {
        var cache = new TemperatureCache(Hct.FromInt(unchecked((int)0xFF6750A4u)));

        // An equal colour, but another instance: the cache keys colours by reference, as upstream's
        // Map does.
        Assert.ThrowsException<ArgumentException>(() => cache.RelativeTemperature(Hct.FromInt(unchecked((int)0xFF6750A4u))));
    }

    [TestMethod]
    [DataRow(10.0, 350.0, 20.0, true)]
    [DataRow(30.0, 350.0, 20.0, false)]
    [DataRow(15.0, 10.0, 20.0, true)]
    [DataRow(25.0, 10.0, 20.0, false)]
    public void IsBetweenRotatesClockwise(double angle, double a, double b, bool expected)
    {
        Assert.AreEqual(expected, TemperatureCache.IsBetween(angle, a, b));
    }

    [TestMethod]
    public void ACacheSharedAcrossThreadsGivesTheSameAnswers()
    {
        var argb = unchecked((int)0xFF4285F4u);
        var alone = new TemperatureCache(Hct.FromInt(argb));
        var expectedComplement = alone.Complement.ToInt();
        var expectedAnalogous = alone.Analogous().Select(h => h.ToInt()).ToArray();
        var shared = new TemperatureCache(Hct.FromInt(argb));

        // Half the threads start with the complement, half with the analogous colours, so both lazy
        // paths race to build the shared lists.
        var results = new (int Complement, int[] Analogous)[16];
        Parallel.For(0, results.Length, i =>
        {
            if (i % 2 == 0)
            {
                var complement = shared.Complement.ToInt();
                results[i] = (complement, [.. shared.Analogous().Select(h => h.ToInt())]);
            }
            else
            {
                int[] analogous = [.. shared.Analogous().Select(h => h.ToInt())];
                results[i] = (shared.Complement.ToInt(), analogous);
            }
        });

        foreach (var (complement, analogous) in results)
        {
            Assert.AreEqual(expectedComplement, complement);
            CollectionAssert.AreEqual(expectedAnalogous, analogous);
        }
    }
}
