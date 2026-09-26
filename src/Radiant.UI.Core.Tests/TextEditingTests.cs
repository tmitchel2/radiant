using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Text;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class TextEditingTests
{
    // "ab[cd]ef" describes text with "cd" selected; "ab[]cd" a caret after "ab".
    private static TextEditState Parse(string marked)
    {
        var start = marked.IndexOf('[', System.StringComparison.Ordinal);
        var end = marked.IndexOf(']', System.StringComparison.Ordinal) - 1;
        var text = marked.Replace("[", "", System.StringComparison.Ordinal).Replace("]", "", System.StringComparison.Ordinal);
        return new TextEditState(text, new TextSelection(start, end));
    }

    private static string Show(TextEditState state) => TextEditing.Describe(state);

    private static Paragraph Laid(TextEditState state, float width = float.PositiveInfinity) =>
        Paragraph.Layout(state.Text, new TextStyle { Size = 16 }, new ParagraphStyle { MaxWidth = width });

    [TestMethod]
    public void TypingReplacesTheSelection()
    {
        Assert.AreEqual("abX[]ef", Show(TextEditing.Insert(Parse("ab[cd]ef"), "X")));
        Assert.AreEqual("abXY[]cd", Show(TextEditing.Insert(Parse("ab[]cd"), "XY")));
    }

    [TestMethod]
    public void BackspaceRemovesAWholeGrapheme()
    {
        var family = "a\U0001F468‍\U0001F469‍\U0001F467";
        var state = new TextEditState(family, TextSelection.Caret(family.Length));

        Assert.AreEqual("a[]", Show(TextEditing.DeleteBackward(state)));
    }

    [TestMethod]
    public void WordDeletionSkipsSpacesAndPunctuation()
    {
        Assert.AreEqual("hello, []", Show(TextEditing.DeleteBackward(Parse("hello, world[]"), byWord: true)));
        Assert.AreEqual("[], world", Show(TextEditing.DeleteForward(Parse("[]hello, world"), byWord: true)));
        Assert.AreEqual("hello[]", Show(TextEditing.DeleteForward(Parse("hello[] world"), byWord: true)));
    }

    [TestMethod]
    public void DeletingWithASelectionDeletesTheSelection()
    {
        Assert.AreEqual("ab[]ef", Show(TextEditing.DeleteBackward(Parse("ab[cd]ef"))));
        Assert.AreEqual("ab[]ef", Show(TextEditing.DeleteForward(Parse("ab[cd]ef"))));
    }

    [TestMethod]
    public void DeletingAtTheEdgesDoesNothing()
    {
        Assert.AreEqual("[]ab", Show(TextEditing.DeleteBackward(Parse("[]ab"))));
        Assert.AreEqual("ab[]", Show(TextEditing.DeleteForward(Parse("ab[]"))));
    }

    [TestMethod]
    public void ArrowsMoveAndShiftExtends()
    {
        var state = Parse("ab[]cd");

        var right = TextEditing.Move(state, Laid(state), CaretMovement.Right, extend: false);
        var extended = TextEditing.Move(state, Laid(state), CaretMovement.Right, extend: true);
        var further = TextEditing.Move(extended, Laid(extended), CaretMovement.Right, extend: true);

        Assert.AreEqual("abc[]d", Show(right));
        Assert.AreEqual("ab[c]d", Show(extended));
        Assert.AreEqual("ab[cd]", Show(further));
        Assert.AreEqual(2, further.Selection.Anchor);
    }

    [TestMethod]
    public void AnArrowWithoutShiftCollapsesTheSelectionToThatSide()
    {
        var state = Parse("a[bcd]e");

        Assert.AreEqual("a[]bcde", Show(TextEditing.Move(state, Laid(state), CaretMovement.Left, extend: false)));
        Assert.AreEqual("abcd[]e", Show(TextEditing.Move(state, Laid(state), CaretMovement.Right, extend: false)));
    }

    [TestMethod]
    public void DoubleAndTripleClickSelectAWordAndALine()
    {
        var state = Parse("[]one two\nthree");
        var paragraph = Laid(state);

        Assert.AreEqual("one [two]\nthree", Show(TextEditing.SelectWord(state, paragraph, 5)));
        Assert.AreEqual("[one two]\nthree", Show(TextEditing.SelectLine(state, paragraph, new TextPosition(2))));
        Assert.AreEqual("[one two\nthree]", Show(TextEditing.SelectAll(state)));
    }

    [TestMethod]
    public void CommandBackspaceDeletesToTheStartOfTheLine()
    {
        var state = Parse("one\ntwo thr[]ee");

        Assert.AreEqual("one\n[]ee", Show(TextEditing.DeleteToLineStart(state, Laid(state))));
    }

    [TestMethod]
    public void AnInputMethodComposesInPlaceUntilCommitted()
    {
        var state = Parse("ab[]cd");

        var first = TextEditing.SetComposing(state, "k", 1, 0);
        var second = TextEditing.SetComposing(first, "か", 1, 0);
        var committed = TextEditing.Insert(second, "感");

        Assert.AreEqual(new TextRange(2, 3), first.Composing);
        Assert.AreEqual("abか[]cd", Show(second));
        Assert.AreEqual(new TextRange(2, 3), second.Composing);
        Assert.AreEqual("ab感[]cd", Show(committed));
        Assert.IsNull(committed.Composing);
    }

    [TestMethod]
    public void CancellingCompositionRemovesTheUnfinishedText()
    {
        var composing = TextEditing.SetComposing(Parse("ab[]"), "xyz", 3, 0);

        var cancelled = TextEditing.SetComposing(composing, "", 0, 0);

        Assert.AreEqual("ab[]", Show(cancelled));
        Assert.IsNull(cancelled.Composing);
    }
}
