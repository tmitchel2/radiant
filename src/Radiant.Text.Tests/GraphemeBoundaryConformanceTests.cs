using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Text.Unicode;

namespace Radiant.Text.Tests;

/// <summary>Grapheme cluster boundaries against every case in Unicode 16.0.0's GraphemeBreakTest.txt.</summary>
[TestClass]
public class GraphemeBoundaryConformanceTests
{
    [TestMethod]
    public void EveryCaseInGraphemeBreakTestMatches()
    {
        var (total, failures, first) = BreakTestFile.Check("GraphemeBreakTest.txt.gz", GraphemeBoundaries.Get);

        Assert.IsTrue(total > 1000, $"Only {total} cases read");
        Assert.AreEqual(0, failures, $"{failures} of {total} differ; first: {first}");
    }
}
