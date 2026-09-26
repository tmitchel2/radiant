using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Platform.Tests;

[TestClass]
public class HeadlessPlatformTests
{
    [TestMethod]
    public void TheInterfaceReachesTheSameParts()
    {
        using var platform = new HeadlessPlatform();
        IPlatform general = platform;

        Assert.AreEqual("headless", general.Name);
        Assert.AreSame(platform.Clipboard, general.Clipboard);
        Assert.AreSame(platform.Cursors, general.Cursors);
        Assert.AreSame(platform.Appearance, general.Appearance);
        Assert.AreSame(platform.Dialogs, general.Dialogs);
        Assert.AreSame(platform.TextInput, general.TextInput);
    }

    [TestMethod]
    public void TheClipboardHoldsWhatWasSet()
    {
        var clipboard = new HeadlessClipboard();
        Assert.IsFalse(clipboard.HasText);
        Assert.IsNull(clipboard.GetText());

        clipboard.SetText("copied ✓");
        Assert.IsTrue(clipboard.HasText);
        Assert.AreEqual("copied ✓", clipboard.GetText());

        clipboard.Clear();
        Assert.IsFalse(clipboard.HasText);
    }

    [TestMethod]
    public void CursorsAreRecordedAndCounted()
    {
        var cursors = new HeadlessCursorService();
        Assert.AreEqual(CursorShape.Arrow, cursors.Current);

        cursors.Show(CursorShape.IBeam);
        cursors.Show(CursorShape.Grab);

        Assert.AreEqual(CursorShape.Grab, cursors.Current);
        Assert.AreEqual(2, cursors.ShowCount);
    }

    [TestMethod]
    public void AppearanceChangesRaiseChangedOnlyWhenSomethingChanges()
    {
        var appearance = new HeadlessAppearance();
        var changes = 0;
        appearance.Changed += () => changes++;

        appearance.IsDark = false; // already light
        Assert.AreEqual(0, changes);

        appearance.IsDark = true;
        appearance.AccentColor = 0xFFFF9500;
        appearance.IncreaseContrast = true;
        appearance.ReduceMotion = true;

        Assert.AreEqual(4, changes);
        Assert.IsTrue(appearance.IsDark);
        Assert.AreEqual(0xFFFF9500u, appearance.AccentColor);
        Assert.IsTrue(appearance.IncreaseContrast);
        Assert.IsTrue(appearance.ReduceMotion);
    }

    [TestMethod]
    public async Task UnscriptedDialogsAreCancelled()
    {
        var dialogs = new HeadlessFileDialogs();

        Assert.AreEqual(0, (await dialogs.OpenAsync()).Count);
        Assert.IsNull(await dialogs.SaveAsync());
    }

    [TestMethod]
    public async Task ScriptedDialogsAnswerWithTheirOptions()
    {
        var dialogs = new HeadlessFileDialogs
        {
            OnOpen = options => options.AllowMultiple ? ["/a.png", "/b.png"] : ["/a.png"],
            OnSave = options => "/tmp/" + options.SuggestedName,
        };

        var opened = await dialogs.OpenAsync(new OpenFileOptions
        {
            AllowMultiple = true,
            Filters = [new FileFilter("Images", ["png", "jpg"])],
        });
        var saved = await dialogs.SaveAsync(new SaveFileOptions { SuggestedName = "Untitled.txt" });

        CollectionAssert.AreEqual(new[] { "/a.png", "/b.png" }, (List<string>)[.. opened]);
        Assert.AreEqual("/tmp/Untitled.txt", saved);
    }

    [TestMethod]
    public void TypingWithoutAClientGoesNowhere()
    {
        var input = new HeadlessTextInput();

        Assert.IsFalse(input.Type("a"));
        Assert.IsFalse(input.Compose("a", 1));
        Assert.IsFalse(input.IsComposing);
    }

    [TestMethod]
    public void ACompositionIsMarkedThenCommitted()
    {
        var input = new HeadlessTextInput();
        var client = new RecordingClient();
        input.Focus(client);

        input.Compose("にほ", 2);
        Assert.IsTrue(input.IsComposing);
        input.Compose("日本", 0, 2);
        input.Type("日本");

        Assert.IsFalse(input.IsComposing);
        CollectionAssert.AreEqual(new[] { "marked:にほ:2:0", "marked:日本:0:2", "insert:日本" }, client.Calls);
    }

    [TestMethod]
    public void AnEmptyCompositionIsACancel()
    {
        var input = new HeadlessTextInput();
        var client = new RecordingClient();
        input.Focus(client);
        input.Compose("あ", 1);

        input.Compose("", 0);

        Assert.IsFalse(input.IsComposing);
    }

    [TestMethod]
    public void UnmarkingEndsTheCompositionOnce()
    {
        var input = new HeadlessTextInput();
        var client = new RecordingClient();
        input.Focus(client);
        input.Compose("あ", 1);

        input.Unmark();
        input.Unmark();

        CollectionAssert.AreEqual(new[] { "marked:あ:1:0", "unmark" }, client.Calls);
    }

    [TestMethod]
    public void MovingFocusEndsTheCompositionKeepingItsText()
    {
        var input = new HeadlessTextInput();
        var first = new RecordingClient();
        var second = new RecordingClient();
        input.Focus(first);
        input.Compose("あ", 1);

        input.Focus(second);
        input.Type("b");

        CollectionAssert.AreEqual(new[] { "marked:あ:1:0", "unmark" }, first.Calls);
        CollectionAssert.AreEqual(new[] { "insert:b" }, second.Calls);
        Assert.AreSame(second, input.Client);
    }

    [TestMethod]
    public void FocusingTheSameClientAgainChangesNothing()
    {
        var input = new HeadlessTextInput();
        var client = new RecordingClient();
        input.Focus(client);
        input.Compose("あ", 1);

        input.Focus(client);

        Assert.IsTrue(input.IsComposing);
    }

    [TestMethod]
    public void CaretInvalidationsAreCounted()
    {
        var input = new HeadlessTextInput();
        input.InvalidateCaret();
        input.InvalidateCaret();

        Assert.AreEqual(2, input.CaretInvalidations);
    }

    private sealed class RecordingClient : ITextInputClient
    {
        public List<string> Calls { get; } = [];

        public RectangleF CaretRect => RectangleF.Empty;

        public void InsertText(string text) => Calls.Add($"insert:{text}");

        public void SetMarkedText(string text, int selectionStart, int selectionLength) =>
            Calls.Add($"marked:{text}:{selectionStart}:{selectionLength}");

        public void UnmarkText() => Calls.Add("unmark");
    }
}
