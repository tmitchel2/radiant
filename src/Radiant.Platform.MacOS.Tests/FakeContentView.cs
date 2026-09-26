using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Radiant.Platform.MacOS.Tests;

/// <summary>
/// An <c>NSView</c> subclass standing in for GLFW's content view: it implements the same
/// methods, each recording that it ran, so tests can check which calls Radiant takes over and
/// which reach the original. Views are made off the main thread and never shown.
/// </summary>
internal static unsafe class FakeContentView
{
    /// <summary>The original methods that ran, by selector, in order.</summary>
    public static List<string> Calls { get; } = [];

    /// <summary>What the original <c>markedRange</c> answers, to tell it from Radiant's.</summary>
    public static readonly NSRange OriginalMarkedRange = new(99, 1);

    /// <summary>What the original <c>firstRectForCharacterRange:actualRange:</c> answers.</summary>
    public static readonly NSRect OriginalRect = new(1, 2, 3, 4);

    private static nint Class => ObjC.DefineSubclass("RadiantFakeContentView", "NSView",
        ("keyDown:", (nint)(delegate* unmanaged<nint, nint, nint, void>)&KeyDown, "v@:@"),
        ("interpretKeyEvents:", (nint)(delegate* unmanaged<nint, nint, nint, void>)&InterpretKeyEvents, "v@:@"),
        ("insertText:replacementRange:", (nint)(delegate* unmanaged<nint, nint, nint, NSRange, void>)&InsertText, "v@:@{_NSRange=QQ}"),
        ("setMarkedText:selectedRange:replacementRange:",
            (nint)(delegate* unmanaged<nint, nint, nint, NSRange, NSRange, void>)&SetMarkedText, "v@:@{_NSRange=QQ}{_NSRange=QQ}"),
        ("unmarkText", (nint)(delegate* unmanaged<nint, nint, void>)&UnmarkText, "v@:"),
        ("hasMarkedText", (nint)(delegate* unmanaged<nint, nint, byte>)&HasMarkedText, "c@:"),
        ("markedRange", (nint)(delegate* unmanaged<nint, nint, NSRange>)&MarkedRange, "{_NSRange=QQ}@:"),
        ("selectedRange", (nint)(delegate* unmanaged<nint, nint, NSRange>)&MarkedRange, "{_NSRange=QQ}@:"),
        ("firstRectForCharacterRange:actualRange:",
            (nint)(delegate* unmanaged<nint, nint, NSRange, NSRange*, NSRect>)&FirstRect, "{CGRect={CGPoint=dd}{CGSize=dd}}@:{_NSRange=QQ}^{_NSRange=QQ}"),
        ("cursorUpdate:", (nint)(delegate* unmanaged<nint, nint, nint, void>)&CursorUpdate, "v@:@"));

    /// <summary>A new view of <paramref name="frame"/> (retained; release when done).</summary>
    public static nint Create(NSRect frame)
    {
        using var pool = ObjC.Pool();
        var view = ObjC.Send(Class, "alloc");
        return ((delegate* unmanaged<nint, nint, NSRect, nint>)ObjC.MsgSend)(view, ObjC.Sel("initWithFrame:"), frame);
    }

    [UnmanagedCallersOnly]
    private static void KeyDown(nint self, nint cmd, nint keyEvent) => Calls.Add("keyDown:");

    [UnmanagedCallersOnly]
    private static void InterpretKeyEvents(nint self, nint cmd, nint events) => Calls.Add("interpretKeyEvents:");

    [UnmanagedCallersOnly]
    private static void InsertText(nint self, nint cmd, nint text, NSRange replacement) =>
        Calls.Add("insertText:" + ObjC.ToManagedString(text));

    [UnmanagedCallersOnly]
    private static void SetMarkedText(nint self, nint cmd, nint text, NSRange selection, NSRange replacement) =>
        Calls.Add("setMarkedText:" + ObjC.ToManagedString(text));

    [UnmanagedCallersOnly]
    private static void UnmarkText(nint self, nint cmd) => Calls.Add("unmarkText");

    [UnmanagedCallersOnly]
    private static byte HasMarkedText(nint self, nint cmd) => 1;

    [UnmanagedCallersOnly]
    private static NSRange MarkedRange(nint self, nint cmd) => OriginalMarkedRange;

    [UnmanagedCallersOnly]
    private static NSRect FirstRect(nint self, nint cmd, NSRange range, NSRange* actual) => OriginalRect;

    [UnmanagedCallersOnly]
    private static void CursorUpdate(nint self, nint cmd, nint cursorEvent) => Calls.Add("cursorUpdate:");
}
