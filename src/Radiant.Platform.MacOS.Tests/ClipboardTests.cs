using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Platform.MacOS.Tests;

[TestClass]
public class ClipboardTests
{
    [TestMethod]
    public void TextRoundTripsThroughAPasteboardExactly()
    {
        MacOnly.Require();
        using var clipboard = MacClipboard.Private();
        Assert.IsFalse(clipboard.HasText);
        Assert.IsNull(clipboard.GetText());

        // Astral characters (emoji), combining marks and CJK all survive, as UTF-16.
        const string text = "Radiant 日本語 e\u0301 \U0001F600\nline two";
        clipboard.SetText(text);

        Assert.IsTrue(clipboard.HasText);
        Assert.AreEqual(text, clipboard.GetText());
    }

    [TestMethod]
    public void SettingTextReplacesWhatWasThere()
    {
        MacOnly.Require();
        using var clipboard = MacClipboard.Private();
        clipboard.SetText("first");
        clipboard.SetText("");

        Assert.AreEqual("", clipboard.GetText());
    }

    [TestMethod]
    [TestCategory(MacOnly.Integration)]
    public void TheSystemClipboardRoundTrips()
    {
        MacOnly.Require();
        // Uses the user's real clipboard, so it restores what was there.
        using var clipboard = MacClipboard.General();
        var saved = clipboard.GetText();
        try
        {
            clipboard.SetText("Radiant clipboard test ✓");
            Assert.AreEqual("Radiant clipboard test ✓", clipboard.GetText());
        }
        finally
        {
            if (saved is not null)
            {
                clipboard.SetText(saved);
            }
        }
    }
}
