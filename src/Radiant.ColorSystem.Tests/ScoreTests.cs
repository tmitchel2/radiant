using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.ColorSystem.Tests;

/// <summary>Upstream's score_test.ts.</summary>
[TestClass]
public class ScoreTests
{
    [TestMethod]
    public void ChromaIsPrioritised()
    {
        var ranked = Rank([(0xff000000, 1), (0xffffffff, 1), (0xff0000ff, 1)], desired: 4);

        AssertRanking(ranked, 0xff0000ff);
    }

    [TestMethod]
    public void ChromaIsPrioritisedWhenProportionsAreEqual()
    {
        var ranked = Rank([(0xffff0000, 1), (0xff00ff00, 1), (0xff0000ff, 1)], desired: 4);

        AssertRanking(ranked, 0xffff0000, 0xff00ff00, 0xff0000ff);
    }

    [TestMethod]
    public void GoogleBlueIsTheFallbackWhenNoColoursAreSuitable()
    {
        var ranked = Rank([(0xff000000, 1)], desired: 4);

        AssertRanking(ranked, 0xff4285f4);
    }

    [TestMethod]
    public void NearbyHuesAreDeduplicated()
    {
        // H 180 C 42 T 50, and H 184 C 35 T 50.
        var ranked = Rank([(0xff008772, 1), (0xff318477, 1)], desired: 4);

        AssertRanking(ranked, 0xff008772);
    }

    [TestMethod]
    public void HueDistanceIsMaximised()
    {
        // H 180 C 42 T 50, H 198 C 50 T 50, and H 245 C 50 T 50.
        var ranked = Rank([(0xff008772, 1), (0xff008587, 1), (0xff007ebc, 1)], desired: 2);

        AssertRanking(ranked, 0xff007ebc, 0xff008772);
    }

    [TestMethod]
    [DataRow(new uint[] { 0xff7ea16d, 0xffd8ccae, 0xff835c0d }, new[] { 67, 67, 49 }, 3, 0xff8d3819, false, new uint[] { 0xff7ea16d, 0xffd8ccae, 0xff835c0d }, DisplayName = "scenario one")]
    [DataRow(new uint[] { 0xffd33881, 0xff3205cc, 0xff0b48cf, 0xffa08f5d }, new[] { 14, 77, 36, 81 }, 4, 0xff7d772b, true, new uint[] { 0xff3205cc, 0xffa08f5d, 0xffd33881 }, DisplayName = "scenario two")]
    [DataRow(new uint[] { 0xffbe94a6, 0xffc33fd7, 0xff899f36, 0xff94c574 }, new[] { 23, 42, 90, 82 }, 3, 0xffaa79a4, true, new uint[] { 0xff94c574, 0xffc33fd7, 0xffbe94a6 }, DisplayName = "scenario three")]
    [DataRow(new uint[] { 0xffdf241c, 0xff685859, 0xffd06d5f, 0xff561c54, 0xff713090 }, new[] { 85, 44, 34, 27, 88 }, 5, 0xff58c19c, false, new uint[] { 0xffdf241c, 0xff561c54 }, DisplayName = "scenario four")]
    [DataRow(new uint[] { 0xffbe66f8, 0xff4bbda9, 0xff80f6f9, 0xffab8017, 0xffe89307 }, new[] { 41, 88, 44, 43, 65 }, 3, 0xff916691, false, new uint[] { 0xffab8017, 0xff4bbda9, 0xffbe66f8 }, DisplayName = "scenario five")]
    [DataRow(new uint[] { 0xff18ea8f, 0xff327593, 0xff066a18, 0xfffa8a23, 0xff04ca1f }, new[] { 93, 18, 53, 74, 62 }, 2, 0xff4c377a, false, new uint[] { 0xff18ea8f, 0xfffa8a23 }, DisplayName = "scenario six")]
    [DataRow(new uint[] { 0xff2e05ed, 0xff153e55, 0xff9ab220, 0xff153379, 0xff68bcc3 }, new[] { 23, 90, 23, 66, 81 }, 2, 0xfff588dc, true, new uint[] { 0xff2e05ed, 0xff9ab220 }, DisplayName = "scenario seven")]
    [DataRow(new uint[] { 0xff816ec5, 0xff6dcb94, 0xff3cae91, 0xff5b542f }, new[] { 24, 19, 98, 25 }, 1, 0xff84b0fd, false, new uint[] { 0xff3cae91 }, DisplayName = "scenario eight")]
    [DataRow(new uint[] { 0xff206f86, 0xff4a620d, 0xfff51401, 0xff2b8ebf, 0xff277766 }, new[] { 52, 96, 85, 3, 59 }, 3, 0xff02b415, true, new uint[] { 0xfff51401, 0xff4a620d, 0xff2b8ebf }, DisplayName = "scenario nine")]
    [DataRow(new uint[] { 0xff8b1d99, 0xff27effe, 0xff6f558d, 0xff77fdf2 }, new[] { 54, 43, 2, 78 }, 4, 0xff5e7a10, true, new uint[] { 0xff27effe, 0xff8b1d99, 0xff6f558d }, DisplayName = "scenario ten")]
    public void GeneratedScenariosRankAsUpstreamDoes(uint[] colors, int[] populations, int desired, uint fallback, bool filter, uint[] expected)
    {
        var ranked = Rank([.. colors.Zip(populations)], desired, fallback, filter);

        CollectionAssert.AreEqual(expected, ranked);
    }

    private static void AssertRanking(uint[] ranked, params uint[] expected) =>
        CollectionAssert.AreEqual(expected, ranked);

    /// <summary>
    /// Ranks colours given as unsigned 0xAARRGGBB, in the given order, and returns the ranking the
    /// same way.
    /// </summary>
    private static uint[] Rank((uint Argb, int Population)[] colorsToPopulation, int desired, uint fallback = 0xff4285f4, bool filter = true)
    {
        var map = new Dictionary<int, int>();
        foreach (var (argb, population) in colorsToPopulation)
        {
            map.Add(unchecked((int)argb), population);
        }
        return [.. Score.Rank(map, desired, unchecked((int)fallback), filter).Select(argb => unchecked((uint)argb))];
    }
}
