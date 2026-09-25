namespace Radiant.Host.Tests;

[TestClass]
public sealed class FollowerWindowSizeTests
{
    private static readonly (int Width, int Height) s_fallback = (1280, 748);

    private static WindowBounds Bounds(int w, int h) =>
        new() { X = 0, Y = 0, Width = w, Height = h, StripHeight = 28f };

    [TestMethod]
    public void UsesSourceHostSizeWhenSessionActiveAndReadable()
    {
        var session = new DragSessionState { Active = true, SourceHost = "radiant-host" };
        var (w, h) = FollowerWindowSize.Resolve(
            session,
            name => name == "radiant-host" ? Bounds(1920, 1080) : null,
            s_fallback);

        Assert.AreEqual(1920, w);
        Assert.AreEqual(1080, h);
    }

    [TestMethod]
    public void FallsBackWhenNoSession()
    {
        var (w, h) = FollowerWindowSize.Resolve(null, _ => Bounds(1920, 1080), s_fallback);
        Assert.AreEqual(s_fallback, (w, h));
    }

    [TestMethod]
    public void FallsBackWhenSessionInactive()
    {
        var session = new DragSessionState { Active = false, SourceHost = "radiant-host" };
        var (w, h) = FollowerWindowSize.Resolve(session, _ => Bounds(1920, 1080), s_fallback);
        Assert.AreEqual(s_fallback, (w, h));
    }

    [TestMethod]
    public void FallsBackWhenSourceBoundsUnreadable()
    {
        var session = new DragSessionState { Active = true, SourceHost = "radiant-host" };
        var (w, h) = FollowerWindowSize.Resolve(session, _ => null, s_fallback);
        Assert.AreEqual(s_fallback, (w, h));
    }

    [TestMethod]
    public void FallsBackWhenSourceHostEmpty()
    {
        var session = new DragSessionState { Active = true, SourceHost = "" };
        var (w, h) = FollowerWindowSize.Resolve(session, _ => Bounds(1920, 1080), s_fallback);
        Assert.AreEqual(s_fallback, (w, h));
    }

    [TestMethod]
    public void FallsBackWhenSourceBoundsDegenerate()
    {
        var session = new DragSessionState { Active = true, SourceHost = "radiant-host" };
        var (w, h) = FollowerWindowSize.Resolve(session, _ => Bounds(0, 0), s_fallback);
        Assert.AreEqual(s_fallback, (w, h));
    }
}
