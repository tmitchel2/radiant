using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Text.Tests;

[TestClass]
public class ParagraphLayoutTests
{
    private const string Pangram = "The quick brown fox jumps over the lazy dog";

    private static TextStyle Body { get; } = new() { Size = 16 };

    private static string LineText(Paragraph paragraph, TextLine line) => paragraph.Text.Text[line.Start..line.End];

    [TestMethod]
    public void EmptyTextHasOneLineAsTallAsItsStyle()
    {
        var paragraph = Paragraph.Layout("", Body);

        Assert.AreEqual(1, paragraph.Lines.Count);
        Assert.AreEqual((0, 0), (paragraph.Lines[0].Start, paragraph.Lines[0].End));
        Assert.AreEqual(Paragraph.Layout("x", Body).Height, paragraph.Height, 1e-3f);
    }

    [TestMethod]
    public void UnwrappedTextIsOneLineAsWideAsItsShapedWidth()
    {
        var paragraph = Paragraph.Layout("Hello, world", Body);
        var inter = FontLibrary.Default.Resolve(FontLibrary.Inter, FontWeight.Regular, italic: false, 16);

        Assert.AreEqual(1, paragraph.Lines.Count);
        Assert.AreEqual(TextShaper.Shape("Hello, world", inter, 16).Width, paragraph.Width, 1e-3f);
        Assert.AreEqual(paragraph.Width, paragraph.LongestLine, 1e-3f);
    }

    [TestMethod]
    public void WrapsBetweenWordsWithinTheWidth()
    {
        var paragraph = Paragraph.Layout(Pangram, Body, new ParagraphStyle { MaxWidth = 120 });

        Assert.IsTrue(paragraph.Lines.Count >= 3, $"{paragraph.Lines.Count} lines");
        Assert.AreEqual(Pangram, string.Concat(paragraph.Lines.Select(l => LineText(paragraph, l))));
        foreach (var line in paragraph.Lines)
        {
            Assert.IsTrue(line.ContentWidth <= 120, $"'{LineText(paragraph, line)}' is {line.ContentWidth}px");
        }
        foreach (var line in paragraph.Lines.SkipLast(1))
        {
            StringAssert.EndsWith(LineText(paragraph, line), " ");
        }
    }

    [TestMethod]
    public void TrailingSpacesHangPastTheWidth()
    {
        var hello = Paragraph.Layout("hello", Body).Width;

        var paragraph = Paragraph.Layout("hello hello", Body, new ParagraphStyle { MaxWidth = hello });

        Assert.AreEqual(2, paragraph.Lines.Count);
        Assert.AreEqual("hello ", LineText(paragraph, paragraph.Lines[0]));
        Assert.AreEqual(hello, paragraph.Lines[0].ContentWidth, 1e-3f);
        Assert.IsTrue(paragraph.Lines[0].Width > hello);
    }

    [TestMethod]
    public void LineBreaksEndLinesAndATrailingOneAddsAnEmptyLine()
    {
        var paragraph = Paragraph.Layout("ab\ncd\r\n", Body);

        Assert.AreEqual(3, paragraph.Lines.Count);
        Assert.AreEqual((0, 3, 1), (paragraph.Lines[0].Start, paragraph.Lines[0].End, paragraph.Lines[0].LineBreakLength));
        Assert.AreEqual((3, 7, 2), (paragraph.Lines[1].Start, paragraph.Lines[1].End, paragraph.Lines[1].LineBreakLength));
        Assert.AreEqual((7, 7), (paragraph.Lines[2].Start, paragraph.Lines[2].End));
    }

    [TestMethod]
    public void LineBreaksDrawNothingAndTakeNoSpace()
    {
        var withBreak = Paragraph.Layout("ab\n", Body);
        var without = Paragraph.Layout("ab", Body);

        Assert.AreEqual(without.Lines[0].Width, withBreak.Lines[0].Width, 1e-3f);
    }

    [TestMethod]
    public void ALongWordBreaksBetweenCharactersWhenAllowed()
    {
        var paragraph = Paragraph.Layout("Antidisestablishmentarianism", Body, new ParagraphStyle { MaxWidth = 60 });

        Assert.IsTrue(paragraph.Lines.Count > 2);
        Assert.IsTrue(paragraph.Lines.All(l => l.ContentWidth <= 60));
    }

    [TestMethod]
    public void ALongWordOverflowsWhenBreakingInsideWordsIsOff()
    {
        var paragraph = Paragraph.Layout("Antidisestablishmentarianism is long", Body,
            new ParagraphStyle { MaxWidth = 60, BreakLongWords = false });

        Assert.AreEqual("Antidisestablishmentarianism ", LineText(paragraph, paragraph.Lines[0]));
        Assert.IsTrue(paragraph.Lines[0].ContentWidth > 60);
    }

    [TestMethod]
    public void MaxLinesCutsTheLastLineAndEndsItWithAnEllipsis()
    {
        var paragraph = Paragraph.Layout(Pangram, Body, new ParagraphStyle { MaxWidth = 120, MaxLines = 2 });

        Assert.AreEqual(2, paragraph.Lines.Count);
        Assert.IsTrue(paragraph.DidExceedMaxLines);
        var last = paragraph.Lines[1];
        Assert.IsTrue(last.IsEllipsized);
        Assert.IsTrue(last.Runs[^1].IsEllipsis);
        Assert.AreEqual("…", string.Concat(last.Runs[^1].Shaped.Glyphs.Select(_ => "…")));
        Assert.IsTrue(last.ContentWidth <= 120, $"{last.ContentWidth}");
        // The last line is filled to the width, not just to its word break.
        Assert.IsTrue(last.ContentWidth > 100, $"{last.ContentWidth}");
    }

    [TestMethod]
    public void ASingleTruncatedLineEllipsizesAtTheWidth()
    {
        var paragraph = Paragraph.Layout(Pangram, Body, new ParagraphStyle { MaxWidth = 150, MaxLines = 1 });

        Assert.AreEqual(1, paragraph.Lines.Count);
        Assert.IsTrue(paragraph.Lines[0].IsEllipsized);
        Assert.IsTrue(paragraph.Lines[0].ContentWidth <= 150);
    }

    [TestMethod]
    public void TextThatFitsIsNotEllipsized()
    {
        var paragraph = Paragraph.Layout("Short", Body, new ParagraphStyle { MaxWidth = 150, MaxLines = 1 });

        Assert.IsFalse(paragraph.DidExceedMaxLines);
        Assert.IsFalse(paragraph.Lines[0].IsEllipsized);
    }

    [TestMethod]
    public void CenterAndRightAlignWithinTheWidth()
    {
        var center = Paragraph.Layout("Hi", Body, new ParagraphStyle { MaxWidth = 200, Alignment = TextAlignment.Center }).Lines[0];
        var right = Paragraph.Layout("Hi", Body, new ParagraphStyle { MaxWidth = 200, Alignment = TextAlignment.Right }).Lines[0];

        Assert.AreEqual((200 - center.ContentWidth) / 2, center.Left, 1e-3f);
        Assert.AreEqual(200, right.Left + right.ContentWidth, 1e-3f);
    }

    [TestMethod]
    public void RightToLeftTextAlignsRightAtItsStart()
    {
        var paragraph = Paragraph.Layout("שלום עולם", Body, new ParagraphStyle { MaxWidth = 200 }, TestLibrary.WithFallbacks);

        var line = paragraph.Lines[0];
        Assert.AreEqual(TextDirection.RightToLeft, line.Direction);
        Assert.AreEqual(200, line.Left + line.Width, 1e-3f);
    }

    [TestMethod]
    public void TrailingSpacesOfRightToLeftTextHangOffTheLeft()
    {
        var hebrew = Paragraph.Layout("שלום", Body, fonts: TestLibrary.WithFallbacks).Width;

        var paragraph = Paragraph.Layout("שלום עולם", Body, new ParagraphStyle { MaxWidth = hebrew }, TestLibrary.WithFallbacks);

        var first = paragraph.Lines[0];
        Assert.AreEqual("שלום ", LineText(paragraph, first));
        Assert.AreEqual(hebrew, first.Left + first.Width, 1e-3f, "the word ends at the right edge");
        Assert.IsTrue(first.Left < 0, "the space hangs off the left");
    }

    [TestMethod]
    public void RightToLeftWordsInLeftToRightTextComeAfterOnScreen()
    {
        var paragraph = Paragraph.Layout("abc שלום def", Body, fonts: TestLibrary.WithFallbacks);

        var runs = paragraph.Lines[0].Runs;
        var hebrew = runs.Single(r => r.Direction == TextDirection.RightToLeft);
        var abc = runs.Single(r => r.Start == 0);
        var def = runs.Single(r => r.End == paragraph.Text.Text.Length);
        Assert.IsTrue(abc.Origin.X < hebrew.Origin.X && hebrew.Origin.X < def.Origin.X);
    }

    [TestMethod]
    public void LeftToRightWordsInRightToLeftTextKeepTheirOrderButSitLeft()
    {
        var paragraph = Paragraph.Layout("שלום abc def", Body, fonts: TestLibrary.WithFallbacks);

        var runs = paragraph.Lines[0].Runs;
        Assert.AreEqual(TextDirection.RightToLeft, paragraph.Lines[0].Direction);
        var latin = runs.Single(r => r.Direction == TextDirection.LeftToRight);
        var hebrew = runs.Single(r => r.Direction == TextDirection.RightToLeft && r.Start == 0);
        Assert.IsTrue(latin.Origin.X < hebrew.Origin.X);
        Assert.AreEqual("abc def", paragraph.Text.Text[latin.Start..latin.End]);
    }

    [TestMethod]
    public void EachLineOfTextFindsItsOwnDirection()
    {
        var paragraph = Paragraph.Layout("Hello\nשלום", Body, fonts: TestLibrary.WithFallbacks);

        Assert.AreEqual(TextDirection.LeftToRight, paragraph.Lines[0].Direction);
        Assert.AreEqual(TextDirection.RightToLeft, paragraph.Lines[1].Direction);
    }

    [TestMethod]
    public void CharactersTheFontLacksFallBackToAFontThatHasThem()
    {
        var paragraph = Paragraph.Layout("Hi שלום", Body, fonts: TestLibrary.WithFallbacks);

        var families = paragraph.Lines[0].Runs.Select(r => r.Shaped.Font.Face.FamilyName).ToHashSet();
        CollectionAssert.AreEquivalent(new[] { FontLibrary.Inter, "Noto Sans Hebrew" }, families.ToArray());
    }

    [TestMethod]
    public void ASpaceBetweenFallbackWordsStaysInTheFallbackRun()
    {
        var paragraph = Paragraph.Layout("שלום עולם", Body, fonts: TestLibrary.WithFallbacks);

        Assert.AreEqual(1, paragraph.Lines[0].Runs.Count);
    }

    [TestMethod]
    public void ASetLineHeightSetsTheLineBoxAndCentresTheText()
    {
        var natural = Paragraph.Layout("Hg", Body).Lines[0];
        var tall = Paragraph.Layout("Hg", Body with { LineHeight = 40 }).Lines[0];

        Assert.AreEqual(40, tall.Height, 1e-3f);
        Assert.AreEqual((40 - natural.Height) / 2, tall.Ascent - natural.Ascent, 1e-3f);
    }

    [TestMethod]
    public void TheTallestTextSetsTheLine()
    {
        var text = new AttributedText.Builder()
            .Append("small ", Body)
            .Append("BIG", Body with { Size = 32 })
            .Build();

        var mixed = Paragraph.Layout(text).Lines[0];
        var big = Paragraph.Layout("BIG", Body with { Size = 32 }).Lines[0];

        Assert.AreEqual(big.Ascent, mixed.Ascent, 1e-3f);
        Assert.AreEqual(2, mixed.Runs.Count);
        Assert.AreEqual(mixed.Runs[0].Origin.Y, mixed.Runs[1].Origin.Y, "one baseline");
    }

    [TestMethod]
    public void LinesStackWithoutGaps()
    {
        var paragraph = Paragraph.Layout(Pangram, Body, new ParagraphStyle { MaxWidth = 100 });

        for (var i = 1; i < paragraph.Lines.Count; i++)
        {
            Assert.AreEqual(paragraph.Lines[i - 1].Bottom, paragraph.Lines[i].Top, 1e-3f);
        }
        Assert.AreEqual(paragraph.Lines[^1].Bottom, paragraph.Height, 1e-3f);
    }

    [TestMethod]
    public void RelayingOutAtAWidthMatchesLayingOutAfresh()
    {
        var wide = Paragraph.Layout(Pangram, Body);

        var narrow = wide.WithMaxWidth(100);
        var fresh = Paragraph.Layout(Pangram, Body, new ParagraphStyle { MaxWidth = 100 });

        CollectionAssert.AreEqual(
            fresh.Lines.Select(l => (l.Start, l.End)).ToArray(),
            narrow.Lines.Select(l => (l.Start, l.End)).ToArray());
        Assert.AreEqual(fresh.Height, narrow.Height, 1e-3f);
    }

    [TestMethod]
    public void IntrinsicWidthsAreTheWidestWordAndTheWidestLine()
    {
        var paragraph = Paragraph.Layout("a quick\nbrown fox", Body);

        var widestWord = new[] { "a", "quick", "brown", "fox" }.Max(w => Paragraph.Layout(w, Body).Width);
        Assert.AreEqual(widestWord, paragraph.MinIntrinsicWidth, 1e-3f);
        Assert.AreEqual(Paragraph.Layout("brown fox", Body).Width, paragraph.MaxIntrinsicWidth, 1e-3f);
    }

    [TestMethod]
    public void ArabicCutByALineBreakTakesItsFinalForm()
    {
        const string word = "بببببببببببببببببببب";
        var fonts = TestLibrary.WithFallbacks;
        var final = GlyphsOf(Paragraph.Layout(word[..2], Body, fonts: fonts).Lines[0].Runs[0].Shaped, 1);
        var medial = GlyphsOf(Paragraph.Layout(word[..3], Body, fonts: fonts).Lines[0].Runs[0].Shaped, 1);
        CollectionAssert.AreNotEqual(final, medial);

        var wrapped = Paragraph.Layout(word, Body, new ParagraphStyle { MaxWidth = 40 }, fonts);

        var first = wrapped.Lines[0];
        Assert.IsTrue(wrapped.Lines.Count > 1);
        CollectionAssert.AreEqual(final, GlyphsOf(first.Runs[0].Shaped, first.End - 1));
    }

    private static uint[] GlyphsOf(ShapedRun run, int cluster) =>
        [.. Enumerable.Range(0, run.Count).Where(i => run.Clusters[i] == cluster).Select(i => run.Glyphs[i])];

    [TestMethod]
    public void TabsAdvanceByTheTabSize()
    {
        var tab = Paragraph.Layout("a\tb", Body).Width;
        var spaces = Paragraph.Layout("a    b", Body).Width;

        Assert.AreEqual(spaces, tab, 1e-3f);
    }

    [TestMethod]
    public void MaxLinesBelowOneKeepsOneLine()
    {
        var paragraph = Paragraph.Layout("a\nb", Body, new ParagraphStyle { MaxLines = 0 });

        Assert.AreEqual(1, paragraph.Lines.Count);
        Assert.IsTrue(Math.Abs(paragraph.Height - paragraph.Lines[0].Height) < 1e-3f);
    }
}
