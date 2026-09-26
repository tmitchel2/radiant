using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Platform.MacOS.Tests;

/// <summary>
/// The <c>NSTextInputClient</c> methods Radiant replaces, called as AppKit would call them, on a
/// stand-in for GLFW's content view. The real view, in a real window, is checked by
/// <c>PlatformCheck --selftest</c>.
/// </summary>
[TestClass]
public unsafe class TextInputTests
{
    private nint _view;
    private MacCursorService _cursors = null!;
    private MacTextInput _input = null!;

    [TestInitialize]
    public void Initialize()
    {
        MacOnly.Require();
        FakeContentView.Calls.Clear();
        _view = FakeContentView.Create(new NSRect(0, 0, 300, 200));
        _cursors = new MacCursorService();
        _input = new MacTextInput(_view, _cursors);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_view != 0)
        {
            _input.Dispose();
            ObjC.Send(_view, "release");
        }
    }

    [TestMethod]
    public void WithoutAClientTheViewsOwnMethodsRun()
    {
        InsertText("a");
        SetMarkedText("に", new NSRange(1, 0));

        CollectionAssert.AreEqual(new[] { "insertText:a", "setMarkedText:に" }, FakeContentView.Calls);
        Assert.IsTrue(HasMarkedText());
        Assert.AreEqual(FakeContentView.OriginalMarkedRange, Range("markedRange"));
        Assert.AreEqual(FakeContentView.OriginalRect, FirstRect(new NSRange(0, 1), out _));
    }

    [TestMethod]
    public void TypedTextGoesToTheClientInstead()
    {
        var client = Focus();

        InsertText("a");

        CollectionAssert.AreEqual(new[] { "insert:a" }, client.Calls);
        Assert.AreEqual(0, FakeContentView.Calls.Count);
    }

    [TestMethod]
    public void ACompositionIsMarkedThenCommitted()
    {
        var client = Focus();

        // Typing "nihon" in Japanese: provisional kana, the caret at the end...
        SetMarkedText("にほん", new NSRange(3, 0));
        Assert.IsTrue(_input.IsComposing);
        Assert.IsTrue(HasMarkedText());
        Assert.AreEqual(new NSRange(0, 3), Range("markedRange"));
        Assert.AreEqual(new NSRange(3, 0), Range("selectedRange"));

        // ...converted, with the input method selecting the clause...
        SetMarkedText("日本", new NSRange(0, 2));
        Assert.AreEqual(new NSRange(0, 2), Range("markedRange"));
        Assert.AreEqual(new NSRange(0, 2), Range("selectedRange"));

        // ...and committed.
        InsertText("日本");
        Assert.IsFalse(_input.IsComposing);
        Assert.IsFalse(HasMarkedText());
        Assert.AreEqual(NSRange.Empty, Range("markedRange"));

        CollectionAssert.AreEqual(new[]
        {
            "marked:にほん:3:0",
            "marked:日本:0:2",
            "insert:日本",
        }, client.Calls);
        Assert.AreEqual(0, FakeContentView.Calls.Count, "GLFW saw none of it");
    }

    [TestMethod]
    public void AttributedMarkedTextIsReadAsItsString()
    {
        var client = Focus();
        using (ObjC.Pool())
        {
            var attributed = ObjC.Send(ObjC.Send(ObjC.Send(ObjC.Class("NSAttributedString"), "alloc"), "initWithString:", ObjC.String("你")), "autorelease");
            Send("setMarkedText:selectedRange:replacementRange:", attributed, new NSRange(1, 0), NSRange.Empty);
        }

        CollectionAssert.AreEqual(new[] { "marked:你:1:0" }, client.Calls);
    }

    [TestMethod]
    public void ASelectionOutsideTheTextIsClampedToIt()
    {
        var client = Focus();

        SetMarkedText("ab", NSRange.Empty);
        SetMarkedText("ab", new NSRange(1, 5));

        CollectionAssert.AreEqual(new[] { "marked:ab:2:0", "marked:ab:1:1" }, client.Calls);
    }

    [TestMethod]
    public void EmptyMarkedTextCancelsTheComposition()
    {
        var client = Focus();
        SetMarkedText("あ", new NSRange(1, 0));

        SetMarkedText("", new NSRange(0, 0));

        Assert.IsFalse(_input.IsComposing);
        Assert.AreEqual("marked::0:0", client.Calls[^1]);
    }

    [TestMethod]
    public void UnmarkingKeepsTheCompositionsText()
    {
        var client = Focus();
        SetMarkedText("あ", new NSRange(1, 0));

        Send("unmarkText");
        Send("unmarkText");

        Assert.IsFalse(_input.IsComposing);
        CollectionAssert.AreEqual(new[] { "marked:あ:1:0", "unmark" }, client.Calls, "a second unmark does nothing");
    }

    [TestMethod]
    public void KeysGoToGlfwUnlessComposing()
    {
        Focus();
        KeyDown();
        CollectionAssert.AreEqual(new[] { "keyDown:" }, FakeContentView.Calls);

        FakeContentView.Calls.Clear();
        SetMarkedText("あ", new NSRange(1, 0));
        KeyDown();

        // While composing only the input method sees the key: GLFW doesn't report it to the UI.
        CollectionAssert.AreEqual(new[] { "interpretKeyEvents:" }, FakeContentView.Calls);
    }

    [TestMethod]
    public void TheCaretIsGivenInAppKitsBottomUpCoordinates()
    {
        var client = Focus();
        client.CaretRect = new System.Drawing.RectangleF(10, 20, 2, 16);

        var rect = FirstRect(new NSRange(0, 3), out var actual);

        // A 200-point-tall unflipped view with no window: y counts up from the bottom.
        Assert.AreEqual(new NSRect(10, 200 - 20 - 16, 2, 16), rect);
        Assert.AreEqual(new NSRange(0, 3), actual);
    }

    [TestMethod]
    public void MovingFocusEndsTheCompositionKeepingItsText()
    {
        var first = Focus();
        SetMarkedText("あ", new NSRange(1, 0));
        var second = new RecordingClient();

        _input.Focus(second);

        Assert.IsFalse(_input.IsComposing);
        CollectionAssert.AreEqual(new[] { "marked:あ:1:0", "unmark" }, first.Calls);
        Assert.AreEqual(0, second.Calls.Count);
        // GLFW's own marked text is cleared too, in case it had some from before.
        CollectionAssert.AreEqual(new[] { "unmarkText" }, FakeContentView.Calls);
    }

    [TestMethod]
    public void WithoutAClientAgainTypingGoesBackToGlfw()
    {
        Focus();
        _input.Focus(null);
        FakeContentView.Calls.Clear();

        InsertText("a");

        CollectionAssert.AreEqual(new[] { "insertText:a" }, FakeContentView.Calls);
    }

    [TestMethod]
    public void ADisposedInputHandsEverythingBack()
    {
        var client = Focus();
        _input.Dispose();
        FakeContentView.Calls.Clear();

        InsertText("a");

        Assert.IsFalse(MacContentView.IsAttached(_view));
        Assert.AreEqual(0, client.Calls.Count);
        CollectionAssert.AreEqual(new[] { "insertText:a" }, FakeContentView.Calls);
    }

    [TestMethod]
    public void FunctionKeyCharactersAreNotText()
    {
        Assert.AreEqual("ab", MacTextInput.Filter("a\uF700b\uF7FF"));
        Assert.AreEqual("日", MacTextInput.Filter("日"));
    }

    [TestMethod]
    public void CursorUpdatesPutBackTheUisCursor()
    {
        _cursors.Show(CursorShape.IBeam);
        using (ObjC.Pool())
        {
            ObjC.Send(ObjC.Send(ObjC.Class("NSCursor"), "arrowCursor"), "set");
        }

        Send("cursorUpdate:", 0);

        using var pool = ObjC.Pool();
        Assert.AreEqual(_cursors.Cursor(CursorShape.IBeam), ObjC.Send(ObjC.Class("NSCursor"), "currentCursor"));
        Assert.AreEqual(0, FakeContentView.Calls.Count, "GLFW's arrow wasn't set");
    }

    // ------------------------------------------------------------------ AppKit's side

    private RecordingClient Focus()
    {
        var client = new RecordingClient();
        _input.Focus(client);
        FakeContentView.Calls.Clear();
        return client;
    }

    private void InsertText(string text)
    {
        using var pool = ObjC.Pool();
        ((delegate* unmanaged<nint, nint, nint, NSRange, void>)ObjC.MsgSend)(
            _view, ObjC.Sel("insertText:replacementRange:"), ObjC.String(text), NSRange.Empty);
    }

    private void SetMarkedText(string text, NSRange selection)
    {
        using var pool = ObjC.Pool();
        Send("setMarkedText:selectedRange:replacementRange:", ObjC.String(text), selection, NSRange.Empty);
    }

    private void Send(string selector, nint text, NSRange selection, NSRange replacement) =>
        ((delegate* unmanaged<nint, nint, nint, NSRange, NSRange, void>)ObjC.MsgSend)(
            _view, ObjC.Sel(selector), text, selection, replacement);

    private void Send(string selector) => ObjC.Send(_view, selector);

    private void Send(string selector, nint arg) => ObjC.Send(_view, selector, arg);

    private void KeyDown()
    {
        using var pool = ObjC.Pool();
        const nuint keyDownType = 10; // NSEventTypeKeyDown
        var a = ObjC.String("a");
        var keyEvent = ((delegate* unmanaged<nint, nint, nuint, Point, nuint, double, nint, nint, nint, nint, byte, ushort, nint>)ObjC.MsgSend)(
            ObjC.Class("NSEvent"),
            ObjC.Sel("keyEventWithType:location:modifierFlags:timestamp:windowNumber:context:characters:charactersIgnoringModifiers:isARepeat:keyCode:"),
            keyDownType, default, 0, 0, 0, 0, a, a, 0, 0);
        Assert.AreNotEqual(0, keyEvent);
        ObjC.Send(_view, "keyDown:", keyEvent);
    }

    private readonly record struct Point(double X, double Y);

    private bool HasMarkedText() => ObjC.GetBool(_view, "hasMarkedText");

    private NSRange Range(string selector) =>
        ((delegate* unmanaged<nint, nint, NSRange>)ObjC.MsgSend)(_view, ObjC.Sel(selector));

    private NSRect FirstRect(NSRange range, out NSRange actual)
    {
        NSRange result = default;
        var rect = ((delegate* unmanaged<nint, nint, NSRange, NSRange*, NSRect>)ObjC.MsgSendStret)(
            _view, ObjC.Sel("firstRectForCharacterRange:actualRange:"), range, &result);
        actual = result;
        return rect;
    }
}
