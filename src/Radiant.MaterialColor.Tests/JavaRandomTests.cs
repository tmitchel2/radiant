using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.MaterialColor.Tests;

/// <summary>
/// <see cref="JavaRandom"/> against outputs of OpenJDK 25's <c>java.util.Random</c>, recorded with
/// jshell.
/// </summary>
[TestClass]
public class JavaRandomTests
{
    [TestMethod]
    public void NextIntMatchesJava()
    {
        var random = new JavaRandom(42);

        Assert.AreEqual(-1170105035, random.NextInt());
        Assert.AreEqual(234785527, random.NextInt());
    }

    [TestMethod]
    [DataRow(42L, 10, new[] { 0, 3, 8, 4, 0, 5, 5, 8, 9, 3 })]
    [DataRow(42L, 100, new[] { 30, 63, 48, 84, 70, 25, 5, 18, 19, 93 })]
    [DataRow(0x42688L, 7, new[] { 5, 1, 1, 2, 0, 1, 3, 4, 1, 3 })]
    public void BoundedNextIntMatchesJava(long seed, int bound, int[] expected)
    {
        var random = new JavaRandom(seed);

        CollectionAssert.AreEqual(expected, Draw(random, bound, expected.Length));
    }

    [TestMethod]
    [DataRow(0x42688L, 16, new[] { 6, 13, 4, 10, 1, 3, 11, 13, 1, 1 })]
    [DataRow(0x42688L, 128, new[] { 49, 111, 34, 84, 13, 30, 91, 105, 9, 12 })]
    public void PowerOfTwoBoundsTakeJavasScaledPath(long seed, int bound, int[] expected)
    {
        var random = new JavaRandom(seed);

        CollectionAssert.AreEqual(expected, Draw(random, bound, expected.Length));
    }

    [TestMethod]
    [DataRow(-1L, int.MaxValue, new[] { 577549913, 943952225, 26349579, 1176895439, 1421815604 })]
    [DataRow(7L, 1073741825, new[] { 20678044, 747989380, 1053566254, 755731200, 259278708 })]
    public void LargeBoundsRejectDrawsAsJavaDoes(long seed, int bound, int[] expected)
    {
        // With a bound just over 2^30, about half of all draws fall in the biased final run and are
        // redrawn, so these only match if the rejection loop does.
        var random = new JavaRandom(seed);

        CollectionAssert.AreEqual(expected, Draw(random, bound, expected.Length));
    }

    [TestMethod]
    public void ReseedingRestartsTheSequence()
    {
        var random = new JavaRandom(1);
        random.NextInt();
        random.SetSeed(42);

        Assert.AreEqual(-1170105035, random.NextInt());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void ANonPositiveBoundIsRejected(int bound)
    {
        var random = new JavaRandom(42);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => random.NextInt(bound));
    }

    private static int[] Draw(JavaRandom random, int bound, int count) =>
        [.. Enumerable.Range(0, count).Select(_ => random.NextInt(bound))];
}
