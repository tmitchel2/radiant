using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Radiant.Platform.MacOS;

/// <summary>
/// Completes GLFW's content view (<c>GLFWContentView</c>) as an <c>NSTextInputClient</c>, by
/// replacing some of its methods at runtime, and keeps the UI's cursor through its cursor
/// updates.
/// <para>
/// <b>Why.</b> GLFW 3.4's view adopts <c>NSTextInputClient</c> but only to receive committed
/// characters (<c>insertText:</c> becomes GLFW's char callback). It stores marked text without
/// telling anyone, reports its marked range one short, says the caret is at the window's origin
/// in <em>window</em> coordinates (so candidate windows appear at the screen's bottom left), and
/// reports every key press to the app before the input method sees it, so Enter that accepts a
/// conversion also reaches the app as Enter.
/// </para>
/// <para>
/// <b>How.</b> <c>class_replaceMethod</c> swaps in <c>[UnmanagedCallersOnly]</c> functions for
/// <c>keyDown:</c>, <c>insertText:replacementRange:</c>,
/// <c>setMarkedText:selectedRange:replacementRange:</c>, <c>unmarkText</c>,
/// <c>hasMarkedText</c>, <c>markedRange</c>, <c>selectedRange</c>,
/// <c>firstRectForCharacterRange:actualRange:</c> and <c>cursorUpdate:</c>, keeping GLFW's
/// implementations. The class is shared by every GLFW window, so each replacement looks up the
/// view it was called on: a view with no <see cref="MacTextInput"/> attached (or whose input has
/// no client) gets GLFW's original, exactly as before. For an attached view with a client:
/// </para>
/// <list type="bullet">
/// <item>the text-input methods go to the <see cref="MacTextInput"/> and not to GLFW, so typed
/// text reaches the client and not GLFW's char callback;</item>
/// <item><c>keyDown:</c> while composing skips GLFW (no key event) and only calls
/// <c>interpretKeyEvents:</c>, so keys the input method uses aren't also acted on by the UI.
/// GLFW ignores a release for a key it never saw pressed, so the matching key-up is dropped
/// too. Outside a composition GLFW's <c>keyDown:</c> runs as normal;</item>
/// <item><c>cursorUpdate:</c> re-applies the UI's cursor rather than GLFW's arrow.</item>
/// </list>
/// </summary>
internal static unsafe class MacContentView
{
    private static readonly Dictionary<nint, Attachment> s_views = [];
    private static readonly Dictionary<nint, Originals> s_classes = [];

    /// <summary>Starts sending <paramref name="view"/>'s text input to <paramref name="input"/>, replacing its class's methods the first time.</summary>
    public static void Attach(nint view, MacTextInput input, MacCursorService? cursors)
    {
        using var pool = ObjC.Pool();
        var cls = ObjC.Send(view, "class");
        if (!s_classes.ContainsKey(cls))
        {
            s_classes[cls] = Install(cls);
        }
        s_views[view] = new Attachment(input, cursors);
    }

    /// <summary>Returns <paramref name="view"/> to GLFW's own behaviour (the methods stay replaced, and pass through).</summary>
    public static void Detach(nint view) => s_views.Remove(view);

    /// <summary>Whether <paramref name="view"/> is attached, for tests.</summary>
    internal static bool IsAttached(nint view) => s_views.ContainsKey(view);

    /// <summary>Empties GLFW's own marked text, left from typing before a client had focus.</summary>
    public static void ClearGlfwMarkedText(nint view)
    {
        if (OriginalsOf(view) is { UnmarkText: not 0 } originals)
        {
            ((delegate* unmanaged<nint, nint, void>)originals.UnmarkText)(view, ObjC.Sel("unmarkText"));
        }
    }

    private static Originals Install(nint cls) => new(
        KeyDown: ObjC.ReplaceMethod(cls, "keyDown:", (nint)(delegate* unmanaged<nint, nint, nint, void>)&KeyDown, "v@:@"),
        InsertText: ObjC.ReplaceMethod(cls, "insertText:replacementRange:",
            (nint)(delegate* unmanaged<nint, nint, nint, NSRange, void>)&InsertText, "v@:@{_NSRange=QQ}"),
        SetMarkedText: ObjC.ReplaceMethod(cls, "setMarkedText:selectedRange:replacementRange:",
            (nint)(delegate* unmanaged<nint, nint, nint, NSRange, NSRange, void>)&SetMarkedText, "v@:@{_NSRange=QQ}{_NSRange=QQ}"),
        UnmarkText: ObjC.ReplaceMethod(cls, "unmarkText", (nint)(delegate* unmanaged<nint, nint, void>)&UnmarkText, "v@:"),
        HasMarkedText: ObjC.ReplaceMethod(cls, "hasMarkedText", (nint)(delegate* unmanaged<nint, nint, byte>)&HasMarkedText, "c@:"),
        MarkedRange: ObjC.ReplaceMethod(cls, "markedRange", (nint)(delegate* unmanaged<nint, nint, NSRange>)&MarkedRange, "{_NSRange=QQ}@:"),
        SelectedRange: ObjC.ReplaceMethod(cls, "selectedRange", (nint)(delegate* unmanaged<nint, nint, NSRange>)&SelectedRange, "{_NSRange=QQ}@:"),
        FirstRect: ObjC.ReplaceMethod(cls, "firstRectForCharacterRange:actualRange:",
            (nint)(delegate* unmanaged<nint, nint, NSRange, NSRange*, NSRect>)&FirstRect,
            "{CGRect={CGPoint=dd}{CGSize=dd}}@:{_NSRange=QQ}^{_NSRange=QQ}"),
        CursorUpdate: ObjC.ReplaceMethod(cls, "cursorUpdate:", (nint)(delegate* unmanaged<nint, nint, nint, void>)&CursorUpdate, "v@:@"));

    // [self class], not object_getClass: key-value observing swaps an observed object's isa for
    // a generated subclass, and -class still answers the real one.
    private static Originals? OriginalsOf(nint view) =>
        s_classes.TryGetValue(ObjC.Send(view, "class"), out var originals) ? originals : null;

    /// <summary>The view's input, if it's attached and a client has focus: then the call is Radiant's to handle.</summary>
    private static MacTextInput? Handling(nint view) =>
        s_views.TryGetValue(view, out var attachment) && attachment.Input.HasClient ? attachment.Input : null;

    // ------------------------------------------------------------------ the replacements

    [UnmanagedCallersOnly]
    private static void KeyDown(nint self, nint cmd, nint keyEvent) => Guard(() =>
    {
        if (Handling(self) is { IsComposing: true })
        {
            using var pool = ObjC.Pool();
            ObjC.Send(self, "interpretKeyEvents:", ObjC.NSArray(keyEvent));
        }
        else if (OriginalsOf(self) is { KeyDown: not 0 } originals)
        {
            ((delegate* unmanaged<nint, nint, nint, void>)originals.KeyDown)(self, cmd, keyEvent);
        }
    });

    [UnmanagedCallersOnly]
    private static void InsertText(nint self, nint cmd, nint text, NSRange replacement) => Guard(() =>
    {
        if (Handling(self) is { } input)
        {
            input.InsertText(text);
        }
        else if (OriginalsOf(self) is { InsertText: not 0 } originals)
        {
            ((delegate* unmanaged<nint, nint, nint, NSRange, void>)originals.InsertText)(self, cmd, text, replacement);
        }
    });

    [UnmanagedCallersOnly]
    private static void SetMarkedText(nint self, nint cmd, nint text, NSRange selection, NSRange replacement) => Guard(() =>
    {
        if (Handling(self) is { } input)
        {
            input.SetMarkedText(text, selection);
        }
        else if (OriginalsOf(self) is { SetMarkedText: not 0 } originals)
        {
            ((delegate* unmanaged<nint, nint, nint, NSRange, NSRange, void>)originals.SetMarkedText)(self, cmd, text, selection, replacement);
        }
    });

    [UnmanagedCallersOnly]
    private static void UnmarkText(nint self, nint cmd) => Guard(() =>
    {
        if (Handling(self) is { } input)
        {
            input.UnmarkText();
        }
        else if (OriginalsOf(self) is { UnmarkText: not 0 } originals)
        {
            ((delegate* unmanaged<nint, nint, void>)originals.UnmarkText)(self, cmd);
        }
    });

    [UnmanagedCallersOnly]
    private static byte HasMarkedText(nint self, nint cmd) => Guard(() =>
    {
        if (Handling(self) is { } input)
        {
            return input.IsComposing ? (byte)1 : (byte)0;
        }
        return OriginalsOf(self) is { HasMarkedText: not 0 } originals
            ? ((delegate* unmanaged<nint, nint, byte>)originals.HasMarkedText)(self, cmd)
            : (byte)0;
    }, (byte)0);

    [UnmanagedCallersOnly]
    private static NSRange MarkedRange(nint self, nint cmd) => Guard(() =>
    {
        if (Handling(self) is { } input)
        {
            return input.MarkedRange;
        }
        return OriginalsOf(self) is { MarkedRange: not 0 } originals
            ? ((delegate* unmanaged<nint, nint, NSRange>)originals.MarkedRange)(self, cmd)
            : NSRange.Empty;
    }, NSRange.Empty);

    [UnmanagedCallersOnly]
    private static NSRange SelectedRange(nint self, nint cmd) => Guard(() =>
    {
        if (Handling(self) is { } input)
        {
            return input.SelectedRange;
        }
        return OriginalsOf(self) is { SelectedRange: not 0 } originals
            ? ((delegate* unmanaged<nint, nint, NSRange>)originals.SelectedRange)(self, cmd)
            : NSRange.Empty;
    }, NSRange.Empty);

    [UnmanagedCallersOnly]
    private static NSRect FirstRect(nint self, nint cmd, NSRange range, NSRange* actual)
    {
        var result = Guard(() =>
        {
            if (Handling(self) is { } input)
            {
                return (Handled: true, Rect: input.CaretScreenRect());
            }
            return (Handled: false, Rect: default(NSRect));
        }, (Handled: false, Rect: default(NSRect)));
        if (result.Handled)
        {
            // The rectangle stands for the whole range asked about.
            if (actual is not null)
            {
                *actual = range;
            }
            return result.Rect;
        }
        return OriginalsOf(self) is { FirstRect: not 0 } originals
            ? ((delegate* unmanaged<nint, nint, NSRange, NSRange*, NSRect>)originals.FirstRect)(self, cmd, range, actual)
            : default;
    }

    [UnmanagedCallersOnly]
    private static void CursorUpdate(nint self, nint cmd, nint cursorEvent) => Guard(() =>
    {
        if (s_views.TryGetValue(self, out var attachment) && attachment.Cursors is { } cursors)
        {
            cursors.Apply();
        }
        else if (OriginalsOf(self) is { CursorUpdate: not 0 } originals)
        {
            ((delegate* unmanaged<nint, nint, nint, void>)originals.CursorUpdate)(self, cmd, cursorEvent);
        }
    });

    // An exception must not unwind into AppKit, which would abort the process: report it and
    // carry on with the fallback.
#pragma warning disable CA1031
    private static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[platform] text input handler failed: {e}");
        }
    }

    private static T Guard<T>(Func<T> action, T fallback)
    {
        try
        {
            return action();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[platform] text input handler failed: {e}");
            return fallback;
        }
    }
#pragma warning restore CA1031

    private sealed record Attachment(MacTextInput Input, MacCursorService? Cursors);

    /// <summary>A class's implementations from before the replacement (zero where it had none).</summary>
    private sealed record Originals(
        nint KeyDown,
        nint InsertText,
        nint SetMarkedText,
        nint UnmarkText,
        nint HasMarkedText,
        nint MarkedRange,
        nint SelectedRange,
        nint FirstRect,
        nint CursorUpdate);
}
