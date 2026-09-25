using System.Text.Json;

namespace Radiant.Host.Tests;

[TestClass]
public sealed class MergeResolutionTests
{
    private static WindowBounds At(int x, int y) =>
        new() { X = x, Y = y, Width = 200, Height = 100, StripHeight = 28f };

    [TestMethod]
    public void ResolveStripTargetPrefersStripBand()
    {
        var others = new (string, WindowBounds)[]
        {
            ("host-a", At(0, 0)),
            ("host-b", At(300, 0)),
        };
        // Cursor inside host-b's strip band.
        Assert.AreEqual("host-b", HostActions.ResolveStripTarget(others, 320, 10));
    }

    [TestMethod]
    public void ResolveStripTargetMatchesStripBandOnly()
    {
        var others = new (string, WindowBounds)[] { ("host-a", At(0, 0)) };
        // Inside the strip band → match.
        Assert.AreEqual("host-a", HostActions.ResolveStripTarget(others, 50, 10));
        // Inside the window body but below the strip → no merge (strip-only).
        Assert.IsNull(HostActions.ResolveStripTarget(others, 50, 60));
        // Outside entirely → null.
        Assert.IsNull(HostActions.ResolveStripTarget(others, 500, 500));
    }

    [TestMethod]
    public void ResolveStripTargetEmptyIsNull()
    {
        Assert.IsNull(HostActions.ResolveStripTarget([], 10, 10));
    }

    [TestMethod]
    public void ResolveStripTargetMarginMatchesNearStrip()
    {
        var others = new (string, WindowBounds)[] { ("host-a", At(0, 0)) };
        // Just below the strip band (y=40, strip=28): strict miss, but within a 24px margin it resolves —
        // so a drag merely NEAR the tabs previews/merges, Chrome-style.
        Assert.IsNull(HostActions.ResolveStripTarget(others, 50, 40));
        Assert.AreEqual("host-a", HostActions.ResolveStripTarget(others, 50, 40, margin: 24f));
        // Past the margin → still null even with the margin (60 >= 28 + 24).
        Assert.IsNull(HostActions.ResolveStripTarget(others, 50, 60, margin: 24f));
    }

    [TestMethod]
    public void ParseAdoptRequestReadsNameAndCursor()
    {
        var p = JsonDocument.Parse("""{"name":"renderer-1","cursorX":123.5}""").RootElement;
        var (name, cursorX) = HostActions.ParseAdoptRequest(p);
        Assert.AreEqual("renderer-1", name);
        Assert.AreEqual(123.5f, cursorX!.Value, 1e-4f);
    }

    [TestMethod]
    public void ParseAdoptRequestCursorOptional()
    {
        var p = JsonDocument.Parse("""{"name":"renderer-1"}""").RootElement;
        var (name, cursorX) = HostActions.ParseAdoptRequest(p);
        Assert.AreEqual("renderer-1", name);
        Assert.IsNull(cursorX);
    }

    [TestMethod]
    public void ParseAdoptRequestWithoutNameThrows()
    {
        var p = JsonDocument.Parse("""{}""").RootElement;
        Assert.ThrowsExactly<ArgumentException>(() => HostActions.ParseAdoptRequest(p));
    }

    [TestMethod]
    public void ParseHandoffRequestReadsNameTargetAndCursor()
    {
        var p = JsonDocument.Parse("""{"name":"r1","target":"host-b","cursorX":42.0}""").RootElement;
        var (name, target, cursorX) = HostActions.ParseHandoffRequest(p);
        Assert.AreEqual("r1", name);
        Assert.AreEqual("host-b", target);
        Assert.AreEqual(42f, cursorX!.Value, 1e-4f);
    }

    [TestMethod]
    public void ParseHandoffRequestWithoutTargetThrows()
    {
        var p = JsonDocument.Parse("""{"name":"r1"}""").RootElement;
        Assert.ThrowsExactly<ArgumentException>(() => HostActions.ParseHandoffRequest(p));
    }
}
