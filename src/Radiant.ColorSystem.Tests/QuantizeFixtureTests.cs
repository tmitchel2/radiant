using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.ColorSystem.Tests;

/// <summary>
/// Quantization and scoring against upstream's Java port, whose seeded k-means this port follows
/// (tools/color-system-fixtures/java/QuantizeFixtures.java).
/// </summary>
[TestClass]
public class QuantizeFixtureTests
{
    private static readonly Lazy<JsonElement[]> s_images =
        new(() => [.. Fixture.Load("quantize").EnumerateArray()]);

    [TestMethod]
    public void TheMapQuantizerCountsEveryColourAsUpstreamDoes()
    {
        ForEachImage((pixels, image) => ComparePairs(image, "map", QuantizerMap.Quantize(pixels)));
    }

    [TestMethod]
    public void WuFindsTheSameClustersInTheSameOrderAsUpstream()
    {
        ForEachImage((pixels, image) => CompareList(image, "wu", new QuantizerWu().Quantize(pixels, 128)));
    }

    [TestMethod]
    public void CelebiMatchesUpstreamOnEveryImage()
    {
        ForEachImage((pixels, image) => ComparePairs(image, "celebi", QuantizerCelebi.Quantize(pixels, 128)));
    }

    [TestMethod]
    public void CelebiWithSixteenColoursMatchesUpstreamOnEveryImage()
    {
        ForEachImage((pixels, image) => ComparePairs(image, "celebi16", QuantizerCelebi.Quantize(pixels, 16)));
    }

    [TestMethod]
    public void ScoringRanksTheSameThemeColoursAsUpstream()
    {
        // Scored from this port's own Celebi result: its order, not just its contents, must match
        // upstream's, since ties between equal scores keep the input's order.
        ForEachImage((pixels, image) =>
        {
            var celebi = QuantizerCelebi.Quantize(pixels, 128);
            return CompareList(image, "score", Score.Rank(celebi))
                ?? CompareList(image, "scoreUnfiltered", Score.Rank(celebi, desired: 6, filter: false))
                ?? CompareList(image, "scoreFallback", Score.Rank(celebi, desired: 3, fallbackColorArgb: unchecked((int)0xff123456)));
        });
    }

    [TestMethod]
    public void ScoringFallsBackOnAGreyscaleImage()
    {
        // The greyscale ramp: every colour fails the chroma filter, so upstream returned the fallback.
        var greyscale = s_images.Value.Single(image => Pixels(image).All(p => IsGrey(p)));
        var celebi = QuantizerCelebi.Quantize(Pixels(greyscale), 128);

        CollectionAssert.AreEqual(new[] { unchecked((int)0xff123456) }, Score.Rank(celebi, desired: 3, fallbackColorArgb: unchecked((int)0xff123456)).ToArray());
    }

    private static bool IsGrey(int argb) =>
        ColorUtils.RedFromArgb(argb) == ColorUtils.GreenFromArgb(argb) && ColorUtils.GreenFromArgb(argb) == ColorUtils.BlueFromArgb(argb);

    private static int[] Pixels(JsonElement image) =>
        [.. image.GetProperty("pixels").EnumerateArray().Select(Fixture.Argb)];

    /// <summary>
    /// Runs <paramref name="check"/> on every image, which returns null or a description of how its
    /// output differs, and fails with how many images differ and the first difference.
    /// </summary>
    private static void ForEachImage(Func<int[], JsonElement, string?> check)
    {
        var images = s_images.Value;
        Assert.IsTrue(images.Length > 0, "the fixture has no images");
        var mismatches = 0;
        var first = "";
        for (var n = 0; n < images.Length; n++)
        {
            var difference = check(Pixels(images[n]), images[n]);
            if (difference != null)
            {
                mismatches++;
                if (first.Length == 0)
                {
                    first = $"image {n}: {difference}";
                }
            }
        }
        Assert.AreEqual(0, mismatches, $"{mismatches} of {images.Length} images differ; first: {first}");
    }

    /// <summary>A map compared as [argb, count] pairs sorted by unsigned colour, as the fixture has it.</summary>
    private static string? ComparePairs(JsonElement image, string field, IReadOnlyDictionary<int, int> actual)
    {
        var expected = image.GetProperty(field).EnumerateArray()
            .Select(pair => (Argb: Fixture.Argb(pair[0]), Count: pair[1].GetInt32()))
            .ToList();
        var sorted = actual.OrderBy(pair => (uint)pair.Key).Select(pair => (Argb: pair.Key, Count: pair.Value)).ToList();
        var differing = 0;
        var first = "";
        for (var i = 0; i < Math.Max(expected.Count, sorted.Count); i++)
        {
            var e = i < expected.Count ? $"{Fixture.Hex(expected[i].Argb)}x{expected[i].Count}" : "nothing";
            var a = i < sorted.Count ? $"{Fixture.Hex(sorted[i].Argb)}x{sorted[i].Count}" : "nothing";
            if (e != a)
            {
                differing++;
                if (first.Length == 0)
                {
                    first = $"entry {i}: expected {e}, got {a}";
                }
            }
        }
        return differing == 0
            ? null
            : $"{field}: {differing} of {Math.Max(expected.Count, sorted.Count)} entries differ ({expected.Count} expected, {sorted.Count} actual); first: {first}";
    }

    /// <summary>A list of colours compared in order.</summary>
    private static string? CompareList(JsonElement image, string field, IReadOnlyList<int> actual)
    {
        var expected = image.GetProperty(field).EnumerateArray().Select(Fixture.Argb).ToList();
        var differing = 0;
        var first = "";
        for (var i = 0; i < Math.Max(expected.Count, actual.Count); i++)
        {
            var e = i < expected.Count ? Fixture.Hex(expected[i]) : "nothing";
            var a = i < actual.Count ? Fixture.Hex(actual[i]) : "nothing";
            if (e != a)
            {
                differing++;
                if (first.Length == 0)
                {
                    first = $"index {i}: expected {e}, got {a}";
                }
            }
        }
        return differing == 0
            ? null
            : $"{field}: {differing} of {Math.Max(expected.Count, actual.Count)} differ ({expected.Count} expected, {actual.Count} actual); first: {first}";
    }
}
