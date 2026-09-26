using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Text.Unicode;

namespace Radiant.Text.Tests;

/// <summary>Word boundaries against every case in Unicode 16.0.0's WordBreakTest.txt.</summary>
[TestClass]
public class WordBoundaryConformanceTests
{
    [TestMethod]
    public void EveryCaseInWordBreakTestMatches()
    {
        var (total, failures, first) = BreakTestFile.Check("WordBreakTest.txt.gz", WordBoundaries.Get);

        Assert.IsTrue(total > 1000, $"Only {total} cases read");
        Assert.AreEqual(0, failures, $"{failures} of {total} differ; first: {first}");
    }
}
