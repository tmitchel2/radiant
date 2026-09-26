using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Text.Unicode;

namespace Radiant.Text.Tests;

[TestClass]
public class GraphemeBoundaryTests
{
    [TestMethod]
    public void AnEmojiZwjSequenceIsOneCharacter()
    {
        const string Family = "\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466";

        CollectionAssert.AreEqual(new[] { 0, Family.Length }, GraphemeBoundaries.Get(Family).ToArray());
    }

    [TestMethod]
    public void RegionalIndicatorsPairIntoFlags()
    {
        // GB, FR, then a lone U.
        const string Text = "\U0001F1EC\U0001F1E7\U0001F1EB\U0001F1F7\U0001F1FA";

        CollectionAssert.AreEqual(new[] { 0, 4, 8, 10 }, GraphemeBoundaries.Get(Text).ToArray());
    }

    [TestMethod]
    public void CombiningMarksSkinTonesAndSurrogatePairsStayWithTheirBase()
    {
        CollectionAssert.AreEqual(new[] { 0, 2, 3 }, GraphemeBoundaries.Get("e\u0301x").ToArray());
        CollectionAssert.AreEqual(new[] { 0, 4 }, GraphemeBoundaries.Get("\U0001F44D\U0001F3FD").ToArray());
        CollectionAssert.AreEqual(new[] { 0, 2, 4 }, GraphemeBoundaries.Get("\U0001D400\U0001D401").ToArray());
    }

    [TestMethod]
    public void CrLfIsOneCharacter()
    {
        CollectionAssert.AreEqual(new[] { 0, 1, 3, 4 }, GraphemeBoundaries.Get("a\r\nb").ToArray());
    }

    [TestMethod]
    public void AnIndicConjunctIsOneCharacter()
    {
        // क्ष: KA, VIRAMA, SSA (GB9c), which .NET's StringInfo splits.
        CollectionAssert.AreEqual(new[] { 0, 3 }, GraphemeBoundaries.Get("क्ष").ToArray());
    }

    [TestMethod]
    public void EmptyTextHasOneBoundary()
    {
        CollectionAssert.AreEqual(new[] { 0 }, GraphemeBoundaries.Get("").ToArray());
        Assert.AreEqual(0, GraphemeBoundaries.Previous("", 0));
        Assert.AreEqual(0, GraphemeBoundaries.Next("", 0));
    }

    [TestMethod]
    public void PreviousAndNextStepOverWholeCharacters()
    {
        const string Text = "e\u0301\U0001F44D\U0001F3FDx";

        Assert.AreEqual(2, GraphemeBoundaries.Next(Text, 0));
        Assert.AreEqual(2, GraphemeBoundaries.Next(Text, 1));
        Assert.AreEqual(6, GraphemeBoundaries.Next(Text, 2));
        Assert.AreEqual(6, GraphemeBoundaries.Previous(Text, 7));
        Assert.AreEqual(2, GraphemeBoundaries.Previous(Text, 6));
        Assert.AreEqual(2, GraphemeBoundaries.Previous(Text, 4));
    }

    [TestMethod]
    public void PreviousAndNextClampAtTheEdges()
    {
        Assert.AreEqual(0, GraphemeBoundaries.Previous("ab", 0));
        Assert.AreEqual(2, GraphemeBoundaries.Next("ab", 2));
        Assert.AreEqual(0, GraphemeBoundaries.Previous("ab", -3));
        Assert.AreEqual(2, GraphemeBoundaries.Next("ab", 9));
    }
}
