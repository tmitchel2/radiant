using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Text.Unicode;

namespace Radiant.Text.Tests;

[TestClass]
public class LineBreakerTests
{
    private static int[] Breaks(string text) => [.. LineBreaker.GetOpportunities(text).Select(b => b.Index)];

    [TestMethod]
    public void EmptyTextHasNoOpportunities()
    {
        Assert.AreEqual(0, LineBreaker.GetOpportunities("").Count);
    }

    [TestMethod]
    public void NullTextIsRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(() => LineBreaker.GetOpportunities(null!));
    }

    [TestMethod]
    public void TheEndOfTextIsAMandatoryBreakAndTheStartIsNever()
    {
        var breaks = LineBreaker.GetOpportunities("word");

        Assert.AreEqual(1, breaks.Count);
        Assert.AreEqual(new LineBreakOpportunity(4, true), breaks[0]);
    }

    [TestMethod]
    public void LinesBreakAfterSpacesNotBeforePunctuation()
    {
        // "Hello, " | "world! " | "(really)?"
        CollectionAssert.AreEqual(new[] { 7, 14, 23 }, Breaks("Hello, world! (really)?"));
        Assert.IsTrue(LineBreaker.GetOpportunities("Hello, world!").All(b => b.Index == 13 || !b.IsMandatory));
    }

    [TestMethod]
    public void RunsOfSpacesStayOnTheLineTheyEnd()
    {
        CollectionAssert.AreEqual(new[] { 4, 5 }, Breaks("a   b"));
    }

    [TestMethod]
    public void CarriageReturnLineFeedIsOneMandatoryBreakAfterTheLineFeed()
    {
        var breaks = LineBreaker.GetOpportunities("one\r\ntwo\nthree");

        CollectionAssert.AreEqual(
            new[] { new LineBreakOpportunity(5, true), new LineBreakOpportunity(9, true), new LineBreakOpportunity(14, true) },
            breaks.ToArray());
    }

    [TestMethod]
    public void TextEndingInALineFeedHasOneBreakAtTheEnd()
    {
        CollectionAssert.AreEqual(new[] { new LineBreakOpportunity(3, true) }, LineBreaker.GetOpportunities("ab\n").ToArray());
    }

    [TestMethod]
    public void LineSeparatorAndNextLineAreMandatory()
    {
        CollectionAssert.AreEqual(new[] { 2, 4 }, LineBreaker.GetOpportunities("a\u2028b\u0085").Where(b => b.IsMandatory).Select(b => b.Index).ToArray());
    }

    [TestMethod]
    public void HyphenatedWordsBreakAfterTheHyphen()
    {
        CollectionAssert.AreEqual(new[] { 5, 10 }, Breaks("well-known"));
    }

    [TestMethod]
    public void AHyphenStartingAWordOrANumberStaysWithIt()
    {
        CollectionAssert.AreEqual(new[] { 2, 5 }, Breaks("a -12"));
        CollectionAssert.AreEqual(new[] { 6, 9 }, Breaks("minus -ab"));
    }

    [TestMethod]
    public void IdeographsBreakBetweenEachOther()
    {
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, Breaks("日本語"));
    }

    [TestMethod]
    public void IdeographicPunctuationDoesNotStartALine()
    {
        // No break before 。 or the closing bracket, nor after the opening one.
        CollectionAssert.AreEqual(new[] { 1, 4, 6 }, Breaks("日「本」語。"));
    }

    [TestMethod]
    public void EmojiZwjSequencesAndSkinTonesStayWhole()
    {
        const string family = "\U0001F468\u200D\U0001F469\u200D\U0001F467";
        const string thumbsUp = "\U0001F44D\U0001F3FD";

        CollectionAssert.AreEqual(new[] { family.Length }, Breaks(family));
        CollectionAssert.AreEqual(new[] { thumbsUp.Length }, Breaks(thumbsUp));
        CollectionAssert.AreEqual(new[] { family.Length, family.Length + thumbsUp.Length }, Breaks(family + thumbsUp));
    }

    [TestMethod]
    public void FlagsBreakBetweenPairsOfRegionalIndicators()
    {
        const string uk = "\U0001F1EC\U0001F1E7";
        const string fr = "\U0001F1EB\U0001F1F7";

        CollectionAssert.AreEqual(new[] { 4, 8 }, Breaks(uk + fr));
    }

    [TestMethod]
    public void NumbersWithCurrencyAndBracketsStayTogether()
    {
        CollectionAssert.AreEqual(new[] { 8 }, Breaks("$(12.35)"));
        CollectionAssert.AreEqual(new[] { 5, 14, 17 }, Breaks("cost $(12.35) now"));
        CollectionAssert.AreEqual(new[] { 12 }, Breaks("₹1,00,000.00"));
    }

    [TestMethod]
    public void SurrogatePairsAreNeverSplit()
    {
        // Mathematical bold letters are letters outside the BMP; CJK Extension B is ideographs.
        CollectionAssert.AreEqual(new[] { 5, 7 }, Breaks("\U0001D400\U0001D401 \U0001D402"));
        CollectionAssert.AreEqual(new[] { 2, 4 }, Breaks("\U00020000\U00020001"));
    }

    [TestMethod]
    public void ALoneSurrogateIsTreatedAsALetter()
    {
        CollectionAssert.AreEqual(new[] { 3 }, Breaks("a\uD800b"));
    }

    [TestMethod]
    public void CombiningMarksStayWithTheirBase()
    {
        // e + combining acute, then a space: the only break is after the space.
        CollectionAssert.AreEqual(new[] { 3, 4 }, Breaks("e\u0301 x"));
    }
}
