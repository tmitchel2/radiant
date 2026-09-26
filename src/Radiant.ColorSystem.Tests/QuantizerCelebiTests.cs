using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.ColorSystem.Tests;

/// <summary>Upstream's quantizer_celebi_test.ts, plus the ways this port's quantizers are defined at the edges.</summary>
[TestClass]
public class QuantizerCelebiTests
{
    private const int Red = unchecked((int)0xffff0000);
    private const int Green = unchecked((int)0xff00ff00);
    private const int Blue = unchecked((int)0xff0000ff);

    [TestMethod]
    [DataRow(Red)]
    [DataRow(Green)]
    [DataRow(Blue)]
    public void ASinglePixelIsItsOwnCluster(int color)
    {
        var answer = QuantizerCelebi.Quantize([color], 128);

        Assert.AreEqual(1, answer.Count);
        Assert.AreEqual(1, answer[color]);
    }

    [TestMethod]
    public void FiveBluePixelsAreOneClusterOfFive()
    {
        var answer = QuantizerCelebi.Quantize([Blue, Blue, Blue, Blue, Blue], 128);

        Assert.AreEqual(1, answer.Count);
        Assert.AreEqual(5, answer[Blue]);
    }

    [TestMethod]
    public void TwoRedsAndThreeGreensAreTwoClusters()
    {
        var answer = QuantizerCelebi.Quantize([Red, Red, Green, Green, Green], 128);

        Assert.AreEqual(2, answer.Count);
        Assert.AreEqual(2, answer[Red]);
        Assert.AreEqual(3, answer[Green]);
    }

    [TestMethod]
    public void RedGreenAndBlueAreThreeClusters()
    {
        var answer = QuantizerCelebi.Quantize([Red, Green, Blue], 128);

        Assert.AreEqual(3, answer.Count);
        Assert.AreEqual(1, answer[Red]);
        Assert.AreEqual(1, answer[Green]);
        Assert.AreEqual(1, answer[Blue]);
    }

    [TestMethod]
    public void AnEmptyImageHasNoClusters()
    {
        Assert.AreEqual(0, QuantizerCelebi.Quantize([], 128).Count);
    }

    [TestMethod]
    public void TheMapQuantizerCountsColoursInTheOrderTheyFirstAppear()
    {
        var answer = QuantizerMap.Quantize([Green, Red, Green, Blue, Red, Green]);

        CollectionAssert.AreEqual(new[] { Green, Red, Blue }, answer.Keys.ToArray());
        Assert.AreEqual(3, answer[Green]);
        Assert.AreEqual(2, answer[Red]);
        Assert.AreEqual(1, answer[Blue]);
    }

    [TestMethod]
    public void WsmeansNeedsStartingClusters()
    {
        Assert.ThrowsException<ArgumentException>(() => QuantizerWsmeans.Quantize([Red, Green], [], 128));
    }
}
