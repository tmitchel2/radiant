using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Text.Tests;

[TestClass]
public class ParagraphCaretTests
{
    private static TextStyle Body { get; } = new() { Size = 16 };

    private static Paragraph Layout(string text, float maxWidth = float.PositiveInfinity) =>
        Paragraph.Layout(text, Body, new ParagraphStyle { MaxWidth = maxWidth }, TestLibrary.WithFallbacks);

    private static float CaretX(Paragraph paragraph, int index, TextAffinity affinity = TextAffinity.Downstream) =>
        paragraph.GetCaretRect(new TextPosition(index, affinity)).Left;

    [TestMethod]
    public void CaretsStepRightThroughLeftToRightText()
    {
        var paragraph = Layout("Hello");

        var xs = Enumerable.Range(0, 6).Select(i => CaretX(paragraph, i)).ToArray();

        Assert.AreEqual(0, xs[0], 1e-3f);
        Assert.AreEqual(paragraph.Width, xs[5], 1e-3f);
        for (var i = 1; i < xs.Length; i++)
        {
            Assert.IsTrue(xs[i] > xs[i - 1], $"caret {i}");
        }
    }

    [TestMethod]
    public void CaretsStepLeftThroughRightToLeftText()
    {
        var paragraph = Layout("שלום");

        var xs = Enumerable.Range(0, 5).Select(i => CaretX(paragraph, i)).ToArray();

        Assert.AreEqual(paragraph.Width, xs[0], 1e-3f);
        Assert.AreEqual(0, xs[4], 1e-3f);
        for (var i = 1; i < xs.Length; i++)
        {
            Assert.IsTrue(xs[i] < xs[i - 1], $"caret {i}");
        }
    }

    [TestMethod]
    public void CaretsGoBetweenTheLettersOfALigature()
    {
        // Inter joins "->" into one arrow glyph, which both characters share.
        var paragraph = Layout("a->b");
        Assert.AreEqual(3, paragraph.Lines[0].Runs[0].Shaped.Count);

        var xs = Enumerable.Range(0, 5).Select(i => CaretX(paragraph, i)).ToArray();

        for (var i = 1; i < xs.Length; i++)
        {
            Assert.IsTrue(xs[i] > xs[i - 1], string.Join(", ", xs));
        }
        Assert.AreEqual(2, paragraph.HitTest(new System.Numerics.Vector2(xs[2], 5)).Index);
    }

    [TestMethod]
    public void TheCaretIsAsTallAsItsLine()
    {
        var paragraph = Layout("one two three", 50);

        var caret = paragraph.GetCaretRect(new TextPosition(paragraph.Lines[1].Start));

        Assert.AreEqual(paragraph.Lines[1].Top, caret.Top, 1e-3f);
        Assert.AreEqual(paragraph.Lines[1].Bottom, caret.Bottom, 1e-3f);
        Assert.AreEqual(0, caret.Width);
    }

    [TestMethod]
    public void ClickingOnACaretFindsItAgain()
    {
        var paragraph = Layout("Hello world, how are you?", 80);

        for (var i = 0; i <= paragraph.Text.Text.Length; i++)
        {
            foreach (var affinity in new[] { TextAffinity.Downstream, TextAffinity.Upstream })
            {
                var position = new TextPosition(i, affinity);
                var caret = paragraph.GetCaretRect(position);
                var hit = paragraph.HitTest(new Vector2(caret.Left, (caret.Top + caret.Bottom) / 2));
                Assert.AreEqual(i, hit.Index, $"index {i} {affinity}");
            }
        }
    }

    [TestMethod]
    public void ClickingOnMixedDirectionTextLandsBesideTheNearestCharacter()
    {
        var paragraph = Layout("abc שלום def");

        for (var i = 0; i <= paragraph.Text.Text.Length; i++)
        {
            var caret = paragraph.GetCaretRect(new TextPosition(i));
            var hit = paragraph.HitTest(new Vector2(caret.Left, caret.Top + 1));
            Assert.AreEqual(caret.Left, paragraph.GetCaretRect(hit).Left, 1e-3f, $"index {i}");
        }
    }

    [TestMethod]
    public void ClickingBesideALineGoesToItsEnds()
    {
        var paragraph = Layout("Hello");

        Assert.AreEqual(0, paragraph.HitTest(new Vector2(-50, 5)).Index);
        Assert.AreEqual(5, paragraph.HitTest(new Vector2(500, 5)).Index);
        Assert.AreEqual(5, paragraph.HitTest(new Vector2(500, 500)).Index, "below the last line");
    }

    [TestMethod]
    public void AWrapHasACaretAtTheEndOfOneLineAndTheStartOfTheNext()
    {
        var paragraph = Layout("hello world", Layout("hello").Width);
        var wrap = paragraph.Lines[1].Start;

        var upstream = paragraph.GetCaretRect(new TextPosition(wrap, TextAffinity.Upstream));
        var downstream = paragraph.GetCaretRect(new TextPosition(wrap, TextAffinity.Downstream));

        Assert.AreEqual(paragraph.Lines[0].Top, upstream.Top);
        Assert.IsTrue(upstream.Left > paragraph.Lines[0].ContentWidth - 1e-3f);
        Assert.AreEqual(paragraph.Lines[1].Top, downstream.Top);
        Assert.AreEqual(0, downstream.Left, 1e-3f);
    }

    [TestMethod]
    public void TheCaretOnAnEmptyLastLineIsBelowTheText()
    {
        var paragraph = Layout("ab\n");

        var caret = paragraph.GetCaretRect(new TextPosition(3));

        Assert.AreEqual(paragraph.Lines[1].Top, caret.Top);
        Assert.AreEqual(0, caret.Left, 1e-3f);
    }

    [TestMethod]
    public void SelectingAcrossLinesGivesABoxPerLine()
    {
        var paragraph = Layout("one two three", 50);

        var boxes = paragraph.GetSelectionRects(0, paragraph.Text.Text.Length);

        Assert.AreEqual(paragraph.Lines.Count, boxes.Count);
        Assert.AreEqual(paragraph.Lines[0].Top, boxes[0].Top);
        Assert.AreEqual(paragraph.Lines[^1].Bottom, boxes[^1].Bottom);
    }

    [TestMethod]
    public void SelectingAcrossADirectionChangeGivesTwoBoxes()
    {
        var paragraph = Layout("abc שלום");

        // "c ש": the end of the Latin, the space and the start of the Hebrew, which is on the far right.
        var boxes = paragraph.GetSelectionRects(2, 5);

        Assert.AreEqual(2, boxes.Count);
        Assert.AreEqual(TextDirection.LeftToRight, boxes[0].Direction);
        Assert.AreEqual(TextDirection.RightToLeft, boxes[1].Direction);
        Assert.AreEqual(paragraph.Width, boxes[1].Right, 1e-3f);
    }

    [TestMethod]
    public void SelectionEndsCanComeInEitherOrder()
    {
        var paragraph = Layout("Hello");

        CollectionAssert.AreEqual(paragraph.GetSelectionRects(1, 4).ToArray(), paragraph.GetSelectionRects(4, 1).ToArray());
    }

    [TestMethod]
    public void ArrowKeysMoveOnScreenInRightToLeftText()
    {
        var paragraph = Layout("שלום");
        var start = new TextPosition(0);

        var left = paragraph.MoveCaret(start, CaretMovement.Left);

        Assert.AreEqual(1, left.Index, "left reads forward in Hebrew");
        Assert.AreEqual(0, paragraph.MoveCaret(left, CaretMovement.Right).Index);
    }

    [TestMethod]
    public void ArrowKeysVisitEveryCaretInLeftToRightText()
    {
        var paragraph = Layout("Hello world, how are you?", 80);
        var position = new TextPosition(0);
        var visited = new System.Collections.Generic.HashSet<int> { 0 };

        for (var i = 0; i < 40; i++)
        {
            position = paragraph.MoveCaret(position, CaretMovement.Right);
            visited.Add(position.Index);
        }

        Assert.AreEqual(paragraph.Text.Text.Length, position.Index);
        Assert.AreEqual(paragraph.Text.Text.Length + 1, visited.Count);
    }

    [TestMethod]
    public void CharacterMovementStepsOverWholeGraphemes()
    {
        const string family = "a\U0001F468\u200D\U0001F469\u200D\U0001F467b";
        var paragraph = Layout(family);

        var next = paragraph.MoveCaret(new TextPosition(1), CaretMovement.NextCharacter);

        Assert.AreEqual(family.Length - 1, next.Index);
        Assert.AreEqual(1, paragraph.MoveCaret(next, CaretMovement.PreviousCharacter).Index);
    }

    [TestMethod]
    public void WordMovementGoesToWordEndsForwardAndStartsBackward()
    {
        var paragraph = Layout("Hello, world again");

        Assert.AreEqual(5, paragraph.MoveCaret(new TextPosition(0), CaretMovement.NextWord).Index);
        Assert.AreEqual(12, paragraph.MoveCaret(new TextPosition(5), CaretMovement.NextWord).Index);
        Assert.AreEqual(12, paragraph.MoveCaret(new TextPosition(8), CaretMovement.NextWord).Index, "mid-word goes to its end");
        Assert.AreEqual(7, paragraph.MoveCaret(new TextPosition(12), CaretMovement.PreviousWord).Index);
        Assert.AreEqual(0, paragraph.MoveCaret(new TextPosition(7), CaretMovement.PreviousWord).Index);
        Assert.AreEqual(18, paragraph.MoveCaret(new TextPosition(13), CaretMovement.WordRight).Index);
    }

    [TestMethod]
    public void UpAndDownKeepTheColumn()
    {
        var paragraph = Layout("abcdef\nab\nabcdef");
        var start = new TextPosition(4);
        var goal = paragraph.GetCaretRect(start).Left;

        var down = paragraph.MoveCaret(start, CaretMovement.Down, goal);
        var downAgain = paragraph.MoveCaret(down, CaretMovement.Down, goal);

        Assert.AreEqual(9, down.Index, "the end of the short line");
        Assert.AreEqual(14, downAgain.Index, "back in the same column");
        Assert.AreEqual(0, paragraph.MoveCaret(new TextPosition(2), CaretMovement.Up).Index, "up from the first line");
        Assert.AreEqual(16, paragraph.MoveCaret(downAgain, CaretMovement.Down).Index, "down from the last line");
    }

    [TestMethod]
    public void LineEndStopsBeforeTheLineBreakOrAtTheWrap()
    {
        var hard = Layout("abc\ndef");
        var soft = Layout("hello world", Layout("hello").Width);

        Assert.AreEqual(3, hard.MoveCaret(new TextPosition(1), CaretMovement.LineEnd).Index);
        var wrapEnd = soft.MoveCaret(new TextPosition(1), CaretMovement.LineEnd);
        Assert.AreEqual(new TextPosition(6, TextAffinity.Upstream), wrapEnd);
        Assert.AreEqual(0, soft.GetLineIndex(wrapEnd));
        Assert.AreEqual(6, soft.MoveCaret(new TextPosition(9), CaretMovement.LineStart).Index);
    }

    [TestMethod]
    public void AWordIsFoundAroundAnIndex()
    {
        var paragraph = Layout("Hello, world");

        Assert.AreEqual(new TextRange(7, 12), paragraph.GetWordRange(9));
        Assert.AreEqual(new TextRange(0, 5), paragraph.GetWordRange(0));
        Assert.AreEqual(new TextRange(7, 12), paragraph.GetWordRange(12));
    }
}
