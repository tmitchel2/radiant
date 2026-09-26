using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Text.Unicode;

namespace Radiant.Text.Tests;

[TestClass]
public class WordBoundaryTests
{
    [TestMethod]
    public void PunctuationAndSpacesAreSegmentsOfTheirOwn()
    {
        CollectionAssert.AreEqual(new[] { "Hello", ",", " ", "world", "!" }, Segments("Hello, world!"));
    }

    [TestMethod]
    public void AnApostropheInsideAWordKeepsItWhole()
    {
        CollectionAssert.AreEqual(new[] { "can't" }, Segments("can't"));
        CollectionAssert.AreEqual(new[] { "don’t", " ", "stop" }, Segments("don’t stop"));
    }

    [TestMethod]
    public void NumbersWithSeparatorsAreOneWord()
    {
        CollectionAssert.AreEqual(new[] { "3.14" }, Segments("3.14"));
        CollectionAssert.AreEqual(new[] { "1,000" }, Segments("1,000"));
        CollectionAssert.AreEqual(new[] { "1,000", "." }, Segments("1,000."));
    }

    [TestMethod]
    public void EmojiSequencesAndFlagsAreNotSplit()
    {
        const string Family = "\U0001F468‍\U0001F469‍\U0001F467";
        const string Flags = "\U0001F1EC\U0001F1E7\U0001F1EB\U0001F1F7";

        CollectionAssert.AreEqual(new[] { Family }, Segments(Family));
        CollectionAssert.AreEqual(new[] { "\U0001F1EC\U0001F1E7", "\U0001F1EB\U0001F1F7" }, Segments(Flags));
    }

    [TestMethod]
    public void EachIdeographIsASegment()
    {
        CollectionAssert.AreEqual(new[] { "日", "本", "語" }, Segments("日本語"));
    }

    [TestMethod]
    public void EmptyTextHasOneBoundary()
    {
        CollectionAssert.AreEqual(new[] { 0 }, WordBoundaries.Get("").ToArray());
        Assert.AreEqual(0, WordBoundaries.Previous("", 0));
        Assert.AreEqual(0, WordBoundaries.Next("", 0));
    }

    [TestMethod]
    public void PreviousAndNextFindTheNeighbouringBoundaries()
    {
        const string Text = "Hello world";

        Assert.AreEqual(5, WordBoundaries.Next(Text, 0));
        Assert.AreEqual(6, WordBoundaries.Next(Text, 5));
        Assert.AreEqual(5, WordBoundaries.Next(Text, 3));
        Assert.AreEqual(6, WordBoundaries.Previous(Text, 11));
        Assert.AreEqual(5, WordBoundaries.Previous(Text, 6));
        Assert.AreEqual(0, WordBoundaries.Previous(Text, 3));
    }

    [TestMethod]
    public void PreviousAndNextClampAtTheEdges()
    {
        const string Text = "Hello world";

        Assert.AreEqual(0, WordBoundaries.Previous(Text, 0));
        Assert.AreEqual(11, WordBoundaries.Next(Text, 11));
        Assert.AreEqual(0, WordBoundaries.Previous(Text, -1));
        Assert.AreEqual(11, WordBoundaries.Next(Text, 20));
    }

    [TestMethod]
    public void OnlyLettersDigitsAndIdeographsMakeAWord()
    {
        Assert.IsTrue(WordBoundaries.IsWord("Hello, world!", 0, 5));
        Assert.IsFalse(WordBoundaries.IsWord("Hello, world!", 5, 6), "comma");
        Assert.IsFalse(WordBoundaries.IsWord("Hello, world!", 6, 7), "space");
        Assert.IsFalse(WordBoundaries.IsWord("   ", 0, 3));
        Assert.IsTrue(WordBoundaries.IsWord("3.14", 0, 4));
        Assert.IsTrue(WordBoundaries.IsWord("日本語", 0, 1));
        Assert.IsTrue(WordBoundaries.IsWord("カタカナ", 0, 4));
        Assert.IsFalse(WordBoundaries.IsWord("\U0001F600", 0, 2), "emoji");
        Assert.IsFalse(WordBoundaries.IsWord("abc", 1, 1), "empty");
    }

    private static string[] Segments(string text)
    {
        var boundaries = WordBoundaries.Get(text);
        return [.. boundaries.Zip(boundaries.Skip(1), (start, end) => text[start..end])];
    }
}
