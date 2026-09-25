using Silk.NET.Input;

namespace Radiant.Host.Tests;

/// <summary>
/// Which keys the compositing host hands on to the tab it is compositing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Until this existed no key reached a tab at all.</b> The host forwarded mouse, scroll and
/// <c>Char</c> — and <c>Char</c> is the printable character a key produced, which Tab, Return, the
/// arrows and every modifier do not have. So a renderer could read typing and could not read a
/// shortcut, and the one shortcut a real tab had bound was dead code nobody had pressed.
/// </para>
/// <para>
/// <b>The set is derived rather than listed</b>, so the assertions here are about the two edges of
/// the derivation: what the host keeps for itself must not go through, and everything else must.
/// </para>
/// </remarks>
[TestClass]
public sealed class LiveHostTests
{
    [TestMethod]
    public void TheKeysATabNeedsForAShortcutAreForwarded()
    {
        Key[] wanted = [Key.Tab, Key.Enter, Key.Escape, Key.Up, Key.Down, Key.Left, Key.Right, Key.G];

        var missing = wanted.Where(key => !LiveHost.ForwardedKeys.Contains(key)).ToList();

        Assert.AreEqual(
            0,
            missing.Count,
            "A tab cannot bind " + string.Join(", ", missing) + " because the host never sends it.");
    }

    [TestMethod]
    public void TheNumberKeysTheHostActsOnItselfAreNotForwarded()
    {
        // The host activates tab N on a digit, unconditionally, before any forwarding happens. Sending
        // it on as well would have one press acted on twice -- by the strip and by the tab.
        Key[] owned =
        [
            Key.Number1, Key.Number2, Key.Number3, Key.Number4, Key.Number5,
            Key.Number6, Key.Number7, Key.Number8, Key.Number9,
        ];

        var leaked = owned.Where(LiveHost.ForwardedKeys.Contains).ToList();

        Assert.AreEqual(
            0,
            leaked.Count,
            "The host acts on " + string.Join(", ", leaked) + " itself and also forwards it.");
    }

    [TestMethod]
    public void NoKeyIsForwardedTwiceAndUnknownIsNotForwardedAtAll()
    {
        // THE FALSIFIER FIRST, because without it this test passes on an enum that has no aliases at
        // all and proves nothing about the Distinct() it exists to defend. Measured: Silk's Key has
        // 122 members and 121 distinct values, Number0 and D0 both being 48.
        Assert.IsTrue(
            Enum.GetValues<Key>().Length > Enum.GetValues<Key>().Distinct().Count(),
            "Key has no aliased members any more, so the assertion below is vacuous.");

        Assert.AreEqual(
            LiveHost.ForwardedKeys.Count,
            LiveHost.ForwardedKeys.Distinct().Count(),
            "A key appears twice, so one press would arrive as two.");

        Assert.IsFalse(LiveHost.ForwardedKeys.Contains(Key.Unknown), "Unknown is not a key.");
    }
}
