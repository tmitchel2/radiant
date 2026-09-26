using System;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Text;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class TextInputTests
{
    private static readonly Vector2 Viewport = new(400, 300);
    private static readonly KeyModifiers Command = OperatingSystem.IsMacOS() ? KeyModifiers.Super : KeyModifiers.Control;

    private sealed record Field(Signal<TextEditState> Value) : Component
    {
        public bool Multiline { get; init; }

        public Action? OnSubmit { get; init; }

        public override Element? Build(BuildContext context) => new Box
        {
            Layout = new LayoutStyle { Padding = Edges.All(10), AlignItems = Align.FlexStart },
            Children =
            [
                new TextInput(context.Watch(Value), next => Value.Value = next)
                {
                    Multiline = Multiline,
                    OnSubmit = OnSubmit,
                    Label = "Name",
                    Style = new TextStyle { Size = 16 },
                    Layout = new LayoutStyle { Width = 300 },
                },
            ],
        };
    }

    private static (UIRoot Root, Signal<TextEditState> Value) Mount(string text = "", bool multiline = false, Action? submit = null)
    {
        var value = new Signal<TextEditState>(TextEditState.From(text));
        var root = new UIRoot(new Field(value) { Multiline = multiline, OnSubmit = submit });
        root.Update(Viewport);
        root.PointerDown(new Vector2(305, 15));
        root.PointerUp(new Vector2(305, 15));
        root.Update(Viewport);
        return (root, value);
    }

    private static void Type(UIRoot root, string text)
    {
        foreach (var c in text)
        {
            root.TextInput(c.ToString());
            root.Update(Viewport);
        }
    }

    private static void Key(UIRoot root, KeyCode key, KeyModifiers modifiers = KeyModifiers.None)
    {
        root.KeyDown(key, modifiers);
        root.Update(Viewport);
    }

    private static string Show(TextEditState state) => TextEditing.Describe(state);

    [TestMethod]
    public void ClickingFocusesAndTypingInserts()
    {
        var (root, value) = Mount();
        using var _ = root;

        Type(root, "Hello");

        Assert.AreEqual("Hello[]", Show(value.Value));
        var field = root.GetSemantics().Children.Single();
        Assert.AreEqual((SemanticsRole.TextField, "Name", "Hello"), (field.Role, field.Label, field.Semantics.Value));
        Assert.IsTrue(field.IsFocused);
    }

    [TestMethod]
    public void ClickingPlacesTheCaretAndDraggingSelects()
    {
        var (root, value) = Mount("Hello world");
        using var _ = root;

        root.PointerDown(new Vector2(11, 15));
        root.Update(Viewport);
        Assert.AreEqual("[]Hello world", Show(value.Value));

        root.PointerMove(new Vector2(250, 15));
        root.Update(Viewport);
        root.PointerUp(new Vector2(250, 15));
        Assert.AreEqual("[Hello world]", Show(value.Value));
    }

    [TestMethod]
    public void ADoubleClickSelectsAWord()
    {
        var (root, value) = Mount("Hello world");
        using var _ = root;
        var now = 0.0;
        root.Clock = () => now;
        var paragraph = Paragraph.Layout("Hello world", new TextStyle { Size = 16 });
        var x = 10 + paragraph.GetCaretRect(new TextPosition(8)).Left;

        root.PointerDown(new Vector2(x, 15));
        root.PointerUp(new Vector2(x, 15));
        now = 0.1;
        root.PointerDown(new Vector2(x, 15));
        root.PointerUp(new Vector2(x, 15));
        root.Update(Viewport);

        Assert.AreEqual("Hello [world]", Show(value.Value));
    }

    [TestMethod]
    public void ArrowsMoveWordsWithOptionAndLineEndsWithCommand()
    {
        var (root, value) = Mount("one two three");
        using var _ = root;

        Key(root, KeyCode.Left, KeyModifiers.Alt);
        Assert.AreEqual("one two []three", Show(value.Value));
        Key(root, KeyCode.Left, KeyModifiers.Alt | KeyModifiers.Shift);
        Assert.AreEqual("one [two ]three", Show(value.Value));
        Key(root, KeyCode.Left, Command);
        Assert.AreEqual("[]one two three", Show(value.Value));
    }

    [TestMethod]
    public void CopyAndPasteGoThroughTheClipboard()
    {
        var (root, value) = Mount("abc");
        using var _ = root;

        Key(root, KeyCode.A, Command);
        Key(root, KeyCode.C, Command);
        Key(root, KeyCode.Right);
        Key(root, KeyCode.V, Command);

        Assert.AreEqual("abcabc[]", Show(value.Value));
    }

    [TestMethod]
    public void CutRemovesTheSelectionOntoTheClipboard()
    {
        var (root, value) = Mount("abc");
        using var _ = root;

        Key(root, KeyCode.Left, KeyModifiers.Shift);
        Key(root, KeyCode.X, Command);
        Key(root, KeyCode.Left, Command);
        Key(root, KeyCode.V, Command);

        Assert.AreEqual("c[]ab", Show(value.Value));
    }

    [TestMethod]
    public void UndoTakesBackARunOfTypingAtOnceAndRedoRestoresIt()
    {
        var (root, value) = Mount();
        using var _ = root;
        Type(root, "abc");
        Type(root, " ");
        Type(root, "de");

        Key(root, KeyCode.Z, Command);
        Assert.AreEqual("abc []", Show(value.Value));
        Key(root, KeyCode.Z, Command);
        Key(root, KeyCode.Z, Command);
        Assert.AreEqual("[]", Show(value.Value));
        Key(root, KeyCode.Z, Command | KeyModifiers.Shift);
        Assert.AreEqual("abc[]", Show(value.Value));
    }

    [TestMethod]
    public void EnterSubmitsAOneLineInputAndBreaksAMultilineOne()
    {
        var submitted = 0;
        var (single, singleValue) = Mount("a", submit: () => submitted++);
        var (multi, multiValue) = Mount("a", multiline: true);
        using var _ = single;
        using var __ = multi;

        Key(single, KeyCode.Enter);
        Key(multi, KeyCode.Enter);

        Assert.AreEqual(1, submitted);
        Assert.AreEqual("a[]", Show(singleValue.Value));
        Assert.AreEqual("a\n[]", Show(multiValue.Value));
    }

    [TestMethod]
    public void BackspaceDeletesAndOptionBackspaceDeletesAWord()
    {
        var (root, value) = Mount("hello big world");
        using var _ = root;

        Key(root, KeyCode.Backspace);
        Key(root, KeyCode.Backspace, KeyModifiers.Alt);

        Assert.AreEqual("hello big []", Show(value.Value));
    }

    [TestMethod]
    public void TheCaretShowsOnlyWhileFocused()
    {
        var (root, _) = Mount("x");
        using var __ = root;
        EditableText Text() => ((EditableTextRenderNode)root.RootRenderNode.Children[0].Children[0].Children[0]).Element;

        Assert.IsTrue(Text().ShowCaret);
        root.ClearFocus();
        root.Update(Viewport);
        Assert.IsFalse(Text().ShowCaret);
    }

    [TestMethod]
    public void AFocusedInputIsThePlatformsTextInputClientAndComposesInPlace()
    {
        var (root, value) = Mount("ab");
        using var _ = root;
        var client = root.TextInputClient;
        Assert.IsNotNull(client, "focused, the input takes input-method text");

        client.SetMarkedText("か", 1, 0);
        root.Update(Viewport);
        Assert.AreEqual(new TextRange(2, 3), value.Value.Composing);
        Assert.IsTrue(client.CaretRect.Height > 0);

        client.InsertText("感");
        root.Update(Viewport);
        Assert.AreEqual("ab感[]", Show(value.Value));
        Assert.IsNull(value.Value.Composing);

        root.ClearFocus();
        root.Update(Viewport);
        Assert.IsNull(root.TextInputClient, "blurred, it lets go");
    }
}
