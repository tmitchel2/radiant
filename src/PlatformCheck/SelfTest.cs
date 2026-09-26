using System;
using System.Collections.Generic;
using System.Drawing;
using Radiant.Platform;
using Radiant.Platform.MacOS;
using Radiant.UI.Core;

namespace Radiant.PlatformCheck;

/// <summary>
/// Checks text input and cursors against GLFW's real content view in a real window, by making
/// the calls AppKit makes: marked text, the caret rectangle, key presses while composing,
/// committed text, cursor updates. Prints PASS or FAIL for each, and exits with the number of
/// failures. What it can't check is an input method itself choosing to make those calls: that
/// is the manual check.
/// </summary>
internal static unsafe class SelfTest
{
    /// <summary>Runs the checks and returns how many failed.</summary>
    /// <param name="platform">The window's platform.</param>
    /// <param name="root">The UI, whose text input client is set.</param>
    /// <param name="keyTarget">A focusable box whose key and text events are recorded in <paramref name="events"/>.</param>
    /// <param name="events">What reached the UI as key and text events.</param>
    public static int Run(IPlatform platform, UIRoot root, ElementRef keyTarget, List<string> events)
    {
        var failures = 0;
        void Check(bool passed, string what)
        {
            Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {what}");
            failures += passed ? 0 : 1;
        }

        if (platform is not MacPlatform mac || mac.View == 0)
        {
            Check(false, $"the window has the macOS platform (it has {platform.Name})");
            return failures;
        }
        using var pool = ObjC.Pool();
        var view = mac.View;
        var window = mac.Window;
        Check(ObjC.ClassName(view) == "GLFWContentView", $"the content view is GLFW's ({ObjC.ClassName(view)})");

        keyTarget.Focus(visible: false);
        var client = new Client { CaretRect = new RectangleF(100, 50, 2, 20) };
        root.TextInputClient = client;
        Check(ReferenceEquals(platform.TextInput.Client, client), "setting the root's client focuses it in the platform");

        // An input method starts composing.
        SetMarkedText(view, "にほ", new NSRange(2, 0));
        Check(client.Last == "marked:にほ:2:0", $"marked text reaches the client ({client.Last})");
        Check(ObjC.GetBool(view, "hasMarkedText"), "the view reports marked text");
        Check(Range(view, "markedRange") == new NSRange(0, 2), $"the marked range is the composition ({Range(view, "markedRange")})");
        Check(Range(view, "selectedRange") == new NSRange(2, 0), "the selected range is the input method's");

        // It asks where to put the candidate window: under the caret, in screen coordinates.
        var rect = FirstRect(view, out var actual);
        var frame = ObjC.GetRect(window, "frame");
        var content = ObjC.GetRect(view, "bounds");
        var expectedTop = frame.Y + content.Height - 50;
        Check(Math.Abs(rect.X - (frame.X + 100)) < 0.5 && Math.Abs(rect.Y + rect.Height - expectedTop) < 0.5 && rect.Height == 20,
            $"the caret rectangle is on screen under the caret ({rect}, window {frame})");
        Check(actual == new NSRange(0, 1), "the actual range is the one asked about");

        // A key pressed while composing is the input method's, not the UI's.
        events.Clear();
        KeyDown(view, window);
        Check(!events.Contains("key:A"), $"a key pressed while composing doesn't reach the UI ({string.Join(",", events)})");

        // It commits (whatever the key did to the composition, a commit ends it).
        InsertText(view, "日本");
        Check(client.Last == "insert:日本", $"committed text reaches the client ({client.Last})");
        Check(!platform.TextInput.IsComposing && !ObjC.GetBool(view, "hasMarkedText"), "the composition has ended");

        // Outside a composition keys reach the UI as usual, and typed text goes to the client only.
        events.Clear();
        KeyDown(view, window);
        Check(events.Contains("key:A"), $"a key pressed outside a composition reaches the UI ({string.Join(",", events)})");
        Check(!events.Exists(e => e.StartsWith("text:", StringComparison.Ordinal)), "text typed with a client focused isn't a UI text event");

        // The UI's cursor survives GLFW's cursor updates.
        platform.Cursors.Show(CursorShape.IBeam);
        var cursor = ObjC.Send(ObjC.Class("NSCursor"), "currentCursor");
        ObjC.Send(ObjC.Send(ObjC.Class("NSCursor"), "arrowCursor"), "set");
        ObjC.Send(view, "cursorUpdate:", 0);
        Check(ObjC.Send(ObjC.Class("NSCursor"), "currentCursor") == cursor, "a cursor update keeps the UI's cursor");
        platform.Cursors.Show(CursorShape.Arrow);

        // Without a client, typing goes back to GLFW's character events.
        root.TextInputClient = null;
        events.Clear();
        InsertText(view, "x");
        Check(platform.TextInput.Client is null, "clearing the root's client unfocuses it");
        Check(events.Contains("text:x"), $"without a client, typed text is a UI text event again ({string.Join(",", events)})");

        Check(platform.Appearance.AccentColor >> 24 == 0xFF, $"the accent colour is read (#{platform.Appearance.AccentColor:X8})");
        return failures;
    }

    private static void SetMarkedText(nint view, string text, NSRange selection) =>
        ((delegate* unmanaged<nint, nint, nint, NSRange, NSRange, void>)ObjC.MsgSend)(
            view, ObjC.Sel("setMarkedText:selectedRange:replacementRange:"), ObjC.String(text), selection, NSRange.Empty);

    private static void InsertText(nint view, string text) =>
        ((delegate* unmanaged<nint, nint, nint, NSRange, void>)ObjC.MsgSend)(
            view, ObjC.Sel("insertText:replacementRange:"), ObjC.String(text), NSRange.Empty);

    private static NSRange Range(nint view, string selector) =>
        ((delegate* unmanaged<nint, nint, NSRange>)ObjC.MsgSend)(view, ObjC.Sel(selector));

    private static NSRect FirstRect(nint view, out NSRange actual)
    {
        NSRange result = default;
        var rect = ((delegate* unmanaged<nint, nint, NSRange, NSRange*, NSRect>)ObjC.MsgSendStret)(
            view, ObjC.Sel("firstRectForCharacterRange:actualRange:"), new NSRange(0, 1), &result);
        actual = result;
        return rect;
    }

    // The A key (virtual key code 0), sent to the view as AppKit would deliver it.
    private static void KeyDown(nint view, nint window)
    {
        const nuint keyDownType = 10; // NSEventTypeKeyDown
        var a = ObjC.String("a");
        var number = ObjC.Send(window, "windowNumber");
        var keyEvent = ((delegate* unmanaged<nint, nint, nuint, Point, nuint, double, nint, nint, nint, nint, byte, ushort, nint>)ObjC.MsgSend)(
            ObjC.Class("NSEvent"),
            ObjC.Sel("keyEventWithType:location:modifierFlags:timestamp:windowNumber:context:characters:charactersIgnoringModifiers:isARepeat:keyCode:"),
            keyDownType, default, 0, 0, number, 0, a, a, 0, 0);
        ObjC.Send(view, "keyDown:", keyEvent);
        // GLFW reports the release too; send it so its key state isn't left pressed.
        var keyUp = ((delegate* unmanaged<nint, nint, nuint, Point, nuint, double, nint, nint, nint, nint, byte, ushort, nint>)ObjC.MsgSend)(
            ObjC.Class("NSEvent"),
            ObjC.Sel("keyEventWithType:location:modifierFlags:timestamp:windowNumber:context:characters:charactersIgnoringModifiers:isARepeat:keyCode:"),
            keyDownType + 1, default, 0, 0, number, 0, a, a, 0, 0);
        ObjC.Send(view, "keyUp:", keyUp);
    }

    private readonly record struct Point(double X, double Y);

    private sealed class Client : ITextInputClient
    {
        public string Last { get; private set; } = "";

        public RectangleF CaretRect { get; set; }

        public void InsertText(string text) => Last = $"insert:{text}";

        public void SetMarkedText(string text, int selectionStart, int selectionLength) =>
            Last = $"marked:{text}:{selectionStart}:{selectionLength}";

        public void UnmarkText() => Last = "unmark";
    }
}
