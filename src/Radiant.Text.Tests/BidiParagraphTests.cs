using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Text.Unicode;

namespace Radiant.Text.Tests;

[TestClass]
public class BidiParagraphTests
{
    private const string Alef = "\u05D0";
    private const string Bet = "\u05D1";
    private const string Gimel = "\u05D2";

    [TestMethod]
    public void LatinIsLeftToRightAtLevelZero()
    {
        var paragraph = BidiParagraph.Resolve("Hello, world!");

        Assert.AreEqual(TextDirection.LeftToRight, paragraph.Direction);
        Assert.AreEqual(0, paragraph.BaseLevel);
        Assert.IsTrue(paragraph.Levels.All(l => l == 0));
        CollectionAssert.AreEqual(Enumerable.Range(0, 13).ToArray(), paragraph.GetVisualOrder(0, 13));
    }

    [TestMethod]
    public void HebrewIsRightToLeftAndReversed()
    {
        var text = Alef + Bet + " " + Gimel;
        var paragraph = BidiParagraph.Resolve(text);

        Assert.AreEqual(TextDirection.RightToLeft, paragraph.Direction);
        Assert.AreEqual(1, paragraph.BaseLevel);
        Assert.IsTrue(paragraph.Levels.All(l => l == 1));
        CollectionAssert.AreEqual(new[] { 3, 2, 1, 0 }, paragraph.GetVisualOrder(0, text.Length));
    }

    [TestMethod]
    public void AnExplicitDirectionOverridesTheFirstStrongCharacter()
    {
        var paragraph = BidiParagraph.Resolve(Alef + "abc", TextDirection.LeftToRight);

        Assert.AreEqual(0, paragraph.BaseLevel);
        CollectionAssert.AreEqual(new byte[] { 1, 0, 0, 0 }, paragraph.Levels.ToArray());
    }

    [TestMethod]
    public void EuropeanNumbersInArabicReadLeftToRight()
    {
        // Three Arabic letters, a space and "123": W2 makes the digits Arabic numbers, which I1 lifts above the Arabic.
        var text = "\u0639\u062F\u062F 123";
        var paragraph = BidiParagraph.Resolve(text);

        CollectionAssert.AreEqual(new byte[] { 1, 1, 1, 1, 2, 2, 2 }, paragraph.Levels.ToArray());
        CollectionAssert.AreEqual(new[] { 4, 5, 6, 3, 2, 1, 0 }, paragraph.GetVisualOrder(0, text.Length));
    }

    [TestMethod]
    public void BracketsEnclosingTheEmbeddingDirectionTakeIt()
    {
        // N0 b: the pair holds an L, so both brackets are L, even though N1 alone would make the
        // closing bracket R between two Hebrew letters.
        var text = Alef + "(a" + Bet + ")" + Gimel;
        var paragraph = BidiParagraph.Resolve(text, TextDirection.LeftToRight);

        CollectionAssert.AreEqual(new byte[] { 1, 0, 0, 1, 0, 1 }, paragraph.Levels.ToArray());
    }

    [TestMethod]
    public void BracketsEnclosingTheOppositeDirectionFollowTheContextBefore()
    {
        // N0 c1: the pair holds only R and R comes before it, so the closing bracket is R too.
        var text = "a " + Alef + "(" + Bet + ") c";
        var paragraph = BidiParagraph.Resolve(text, TextDirection.LeftToRight);

        CollectionAssert.AreEqual(new byte[] { 0, 0, 1, 1, 1, 1, 0, 0 }, paragraph.Levels.ToArray());
    }

    [TestMethod]
    public void CanonicallyEquivalentAngleBracketsPair()
    {
        // U+2329 is canonically equivalent to U+3008, so it pairs with U+3009.
        var text = Alef + "\u2329a" + Bet + "\u3009" + Gimel;
        var paragraph = BidiParagraph.Resolve(text, TextDirection.LeftToRight);

        CollectionAssert.AreEqual(new byte[] { 1, 0, 0, 1, 0, 1 }, paragraph.Levels.ToArray());
    }

    [TestMethod]
    public void RightToLeftIsolateKeepsItsContentApart()
    {
        // RLI … PDI: the Hebrew sits at level 1 while the isolate marks stay at the paragraph level.
        var text = "a \u2067" + Alef + Bet + "\u2069 b";
        var paragraph = BidiParagraph.Resolve(text);

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 1, 1, 0, 0, 0 }, paragraph.Levels.ToArray());
    }

    [TestMethod]
    public void FirstStrongIsolateTakesItsContentsDirection()
    {
        var hebrew = BidiParagraph.Resolve("a \u2068" + Alef + "b\u2069");
        var latin = BidiParagraph.Resolve(Alef + " \u2068b" + Alef + "\u2069", TextDirection.RightToLeft);

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 1, 2, 0 }, hebrew.Levels.ToArray());
        CollectionAssert.AreEqual(new byte[] { 1, 1, 1, 2, 3, 1 }, latin.Levels.ToArray());
    }

    [TestMethod]
    public void TheParagraphDirectionSkipsIsolates()
    {
        var paragraph = BidiParagraph.Resolve("\u2067" + Alef + "\u2069abc");

        Assert.AreEqual(TextDirection.LeftToRight, paragraph.Direction);
    }

    [TestMethod]
    public void LeftToRightIsolateInRightToLeftText()
    {
        var text = Alef + " \u2066ab\u2069 " + Bet;
        var paragraph = BidiParagraph.Resolve(text);

        CollectionAssert.AreEqual(new byte[] { 1, 1, 1, 2, 2, 1, 1, 1 }, paragraph.Levels.ToArray());
        CollectionAssert.AreEqual(new[] { 7, 6, 5, 3, 4, 2, 1, 0 }, paragraph.GetVisualOrder(0, text.Length));
    }

    [TestMethod]
    public void TrailingWhitespaceReturnsToTheParagraphLevel()
    {
        // "Alef abc def" broken after "abc ": that space sits between two Latin words (level 2) until
        // it ends the line, where L1 puts it back at the paragraph level.
        var text = Alef + " abc def";
        var paragraph = BidiParagraph.Resolve(text);

        Assert.AreEqual(2, paragraph.Levels[5]);
        CollectionAssert.AreEqual(new byte[] { 1, 1, 2, 2, 2, 1 }, paragraph.GetLineLevels(0, 6));
        CollectionAssert.AreEqual(new byte[] { 2, 2, 2 }, paragraph.GetLineLevels(6, 9));
        CollectionAssert.AreEqual(new[] { 5, 2, 3, 4, 1, 0 }, paragraph.GetVisualOrder(0, 6));
    }

    [TestMethod]
    public void SegmentSeparatorsAndWhitespaceBeforeThemReturnToTheParagraphLevel()
    {
        // Inside a right-to-left override, a tab and the spaces before it still go to level 0.
        var text = "\u202E" + "ab \t" + "cd";
        var paragraph = BidiParagraph.Resolve(text, TextDirection.LeftToRight);

        CollectionAssert.AreEqual(new byte[] { 0, 1, 1, 1, 1, 1, 1 }, paragraph.Levels.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0, 1, 1, 0, 0, 1, 1 }, paragraph.GetLineLevels(0, text.Length));
    }

    [TestMethod]
    public void MixedLineIsDisplayedInVisualOrder()
    {
        var text = "abc " + Alef + Bet + Gimel + " def";
        var paragraph = BidiParagraph.Resolve(text);

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 6, 5, 4, 7, 8, 9, 10 }, paragraph.GetVisualOrder(0, text.Length));
    }

    [TestMethod]
    public void SurrogatePairsKeepTheirHalvesTogether()
    {
        // Cypriot U+10800 and U+10801 are R and outside the BMP.
        var text = "a \U00010800\U00010801 b";
        var paragraph = BidiParagraph.Resolve(text);

        CollectionAssert.AreEqual(new byte[] { 0, 0, 1, 1, 1, 1, 0, 0 }, paragraph.Levels.ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1, 4, 5, 2, 3, 6, 7 }, paragraph.GetVisualOrder(0, text.Length));
    }

    [TestMethod]
    public void SurrogatePairsDecideTheParagraphDirection()
    {
        var text = "\U00010800\U00010801";
        var paragraph = BidiParagraph.Resolve(text);

        Assert.AreEqual(TextDirection.RightToLeft, paragraph.Direction);
        CollectionAssert.AreEqual(new byte[] { 1, 1, 1, 1 }, paragraph.GetLineLevels(0, 4));
        CollectionAssert.AreEqual(new[] { 2, 3, 0, 1 }, paragraph.GetVisualOrder(0, 4));
    }

    [TestMethod]
    public void ParenthesesMirrorOnlyWhenRightToLeft()
    {
        var rtl = BidiParagraph.Resolve(Alef + "(" + Bet + ")-");
        var ltr = BidiParagraph.Resolve("a(b)");

        Assert.IsTrue(rtl.IsMirrored(1));
        Assert.IsTrue(rtl.IsMirrored(3));
        Assert.IsFalse(rtl.IsMirrored(0), "letters are not mirrored");
        Assert.IsFalse(rtl.IsMirrored(4), "a hyphen is not Bidi_Mirrored");
        Assert.IsFalse(ltr.IsMirrored(1));
        Assert.IsFalse(ltr.IsMirrored(3));
    }

    [TestMethod]
    public void ReorderLevelsReversesEachLevelInTurn()
    {
        CollectionAssert.AreEqual(new[] { 0, 5, 3, 4, 2, 1, 6 }, BidiParagraph.ReorderLevels([0, 1, 1, 2, 2, 1, 0]));
        Assert.AreEqual(0, BidiParagraph.ReorderLevels([]).Length);
    }

    [TestMethod]
    public void ParagraphSeparatorEndsEmbeddingsButKeepsTheBaseLevel()
    {
        // RLE "ab" LF "cd": the separator closes the embedding, so "cd" is back at level 0.
        var text = "\u202Bab\ncd";
        var paragraph = BidiParagraph.Resolve(text, TextDirection.LeftToRight);

        CollectionAssert.AreEqual(new byte[] { 0, 2, 2, 0, 0, 0 }, paragraph.Levels.ToArray());
        Assert.AreEqual(0, BidiParagraph.Resolve("abc\u2029" + Alef).BaseLevel, "the first paragraph decides");
    }

    [TestMethod]
    public void ExplicitEmbeddingsDeeperThanTheMaximumOverflow()
    {
        var text = new StringBuilder();
        for (var i = 0; i < 130; i++)
        {
            text.Append('\u202A');
        }
        text.Append('a');
        var paragraph = BidiParagraph.Resolve(text.ToString());

        Assert.AreEqual(124, paragraph.Levels[^1], "LRE stops at the deepest even level");
    }

    [TestMethod]
    public void HundredThousandCharactersResolveQuickly()
    {
        var text = new StringBuilder();
        while (text.Length < 100_000)
        {
            text.Append("The quick (brown) fox ").Append(Alef + Bet + Gimel + " 123, ").Append("\u0639\u062F\u062F [45.6] ");
        }
        var s = text.ToString();
        BidiParagraph.Resolve(s).GetVisualOrder(0, s.Length);

        var clock = Stopwatch.StartNew();
        var paragraph = BidiParagraph.Resolve(s);
        paragraph.GetVisualOrder(0, s.Length);
        clock.Stop();

        // Generous for debug builds and busy machines; a release build takes a few milliseconds.
        Assert.IsTrue(clock.ElapsedMilliseconds < 200, $"{clock.ElapsedMilliseconds} ms");
    }
}
