using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.MaterialColor.Tests;

/// <summary>Upstream's contrast_test.ts.</summary>
[TestClass]
public class ContrastTests
{
    private const double Delta = 0.001;

    [TestMethod]
    public void RatioOfTonesClampsOutOfBoundsInput()
    {
        Assert.AreEqual(21.0, Contrast.RatioOfTones(-10.0, 110.0), Delta);
    }

    [TestMethod]
    public void LighterFailsForAnImpossibleRatio()
    {
        Assert.AreEqual(-1.0, Contrast.Lighter(90.0, 10.0), Delta);
    }

    [TestMethod]
    public void LighterFailsForATooHighTone()
    {
        Assert.AreEqual(-1.0, Contrast.Lighter(110.0, 2.0), Delta);
    }

    [TestMethod]
    public void LighterFailsForATooLowTone()
    {
        Assert.AreEqual(-1.0, Contrast.Lighter(-10.0, 2.0), Delta);
    }

    [TestMethod]
    public void LighterUnsafeFallsBackToTheMaximumTone()
    {
        Assert.AreEqual(100.0, Contrast.LighterUnsafe(100.0, 2.0), Delta);
    }

    [TestMethod]
    public void DarkerFailsForAnImpossibleRatio()
    {
        Assert.AreEqual(-1.0, Contrast.Darker(10.0, 20.0), Delta);
    }

    [TestMethod]
    public void DarkerFailsForATooHighTone()
    {
        Assert.AreEqual(-1.0, Contrast.Darker(110.0, 2.0), Delta);
    }

    [TestMethod]
    public void DarkerFailsForATooLowTone()
    {
        Assert.AreEqual(-1.0, Contrast.Darker(-10.0, 2.0), Delta);
    }

    [TestMethod]
    public void DarkerUnsafeFallsBackToTheMinimumTone()
    {
        Assert.AreEqual(0.0, Contrast.DarkerUnsafe(0.0, 2.0), Delta);
    }
}
