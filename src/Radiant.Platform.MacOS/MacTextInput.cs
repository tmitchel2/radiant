using System;
using System.Text;

namespace Radiant.Platform.MacOS;

/// <summary>
/// Text input for one GLFW window, through AppKit's <c>NSTextInputClient</c> protocol on its
/// content view. <see cref="MacContentView"/> sends the protocol's calls here while a client
/// has focus; this keeps the composition's length and selection (all AppKit asks about) and
/// passes the text on to the client.
/// <para>
/// The "document" AppKit sees is only the composition: its marked range starts at 0 and the
/// selected range is the input method's selection within it. Clients don't expose their whole
/// text, so input-method features that read around the caret (reconversion, context-aware
/// prediction) get nothing; see the improvements log.
/// </para>
/// </summary>
internal sealed class MacTextInput : ITextInput, IDisposable
{
    // NSEventModifierFlagCommand: text typed with ⌘ is a shortcut, not text, as GLFW decides too.
    private const nuint CommandFlag = 1 << 20;

    private readonly nint _view;
    private int _markedLength;
    private NSRange _selection;
    private bool _discarding;
    private bool _disposed;

    /// <summary>Takes over text input on <paramref name="view"/>, a GLFW content view.</summary>
    /// <param name="view">The <c>NSView</c> that is the window's first responder.</param>
    /// <param name="cursors">Cursors to restore when the view resets the cursor, if any.</param>
    public MacTextInput(nint view, MacCursorService? cursors)
    {
        _view = view;
        MacContentView.Attach(view, this, cursors);
    }

    /// <inheritdoc/>
    public ITextInputClient? Client { get; private set; }

    /// <inheritdoc/>
    public bool IsComposing => _markedLength > 0;

    /// <inheritdoc/>
    public void Focus(ITextInputClient? client)
    {
        if (ReferenceEquals(client, Client))
        {
            return;
        }
        var previous = Client;
        var wasComposing = IsComposing;
        _markedLength = 0;
        _selection = default;
        Client = client;
        if (wasComposing)
        {
            previous?.UnmarkText();
        }
        // Reset the input method, so a composition begun for one client doesn't carry on in
        // the next, and clear GLFW's own record of marked text from before a client had focus.
        using var pool = ObjC.Pool();
        _discarding = true;
        try
        {
            ObjC.Send(InputContext, "discardMarkedText");
            MacContentView.ClearGlfwMarkedText(_view);
        }
        finally
        {
            _discarding = false;
        }
    }

    /// <inheritdoc/>
    public void InvalidateCaret()
    {
        using var pool = ObjC.Pool();
        ObjC.Send(InputContext, "invalidateCharacterCoordinates");
    }

    private nint InputContext => ObjC.Send(_view, "inputContext");

    // ------------------------------------------------------------------ from NSTextInputClient

    /// <summary>Whether AppKit's calls should come here rather than to GLFW.</summary>
    internal bool HasClient => Client is not null && !_disposed;

    internal NSRange MarkedRange => IsComposing ? new NSRange(0, (nuint)_markedLength) : NSRange.Empty;

    internal NSRange SelectedRange => IsComposing ? _selection : new NSRange(0, 0);

    /// <summary><c>insertText:replacementRange:</c>: committed text.</summary>
    internal void InsertText(nint text)
    {
        if (_discarding || Client is not { } client)
        {
            return;
        }
        var value = Filter(ObjC.ToManagedString(text) ?? "");
        if (value.Length == 0 && !IsComposing)
        {
            return;
        }
        // Typing with ⌘ held is a key equivalent GLFW reports as a key, not text.
        var app = ObjC.AppKitConstant("NSApp");
        var current = app == 0 ? 0 : ObjC.Send(app, "currentEvent");
        if (!IsComposing && current != 0 && ((nuint)ObjC.Send(current, "modifierFlags") & CommandFlag) != 0)
        {
            return;
        }
        _markedLength = 0;
        _selection = default;
        client.InsertText(value);
    }

    /// <summary><c>setMarkedText:selectedRange:replacementRange:</c>: provisional text.</summary>
    internal void SetMarkedText(nint text, NSRange selection)
    {
        if (_discarding || Client is not { } client)
        {
            return;
        }
        var value = ObjC.ToManagedString(text) ?? "";
        var start = selection.Location == NSRange.NotFound ? value.Length : (int)Math.Min(selection.Location, (nuint)value.Length);
        var length = (int)Math.Min(selection.Length, (nuint)(value.Length - start));
        _markedLength = value.Length;
        _selection = new NSRange((nuint)start, (nuint)length);
        client.SetMarkedText(value, start, length);
    }

    /// <summary><c>unmarkText</c>: the composition ends and keeps its text.</summary>
    internal void UnmarkText()
    {
        if (_discarding || Client is not { } client || !IsComposing)
        {
            return;
        }
        _markedLength = 0;
        _selection = default;
        client.UnmarkText();
    }

    /// <summary>
    /// <c>firstRectForCharacterRange:actualRange:</c>: where the candidate window goes, in screen
    /// coordinates (points, bottom-left origin). This is the client's caret, converted from the
    /// UI's top-left coordinates through the view and window.
    /// </summary>
    internal NSRect CaretScreenRect()
    {
        var caret = Client?.CaretRect ?? default;
        var bounds = ObjC.GetRect(_view, "bounds");
        var flipped = ObjC.GetBool(_view, "isFlipped");
        var local = new NSRect(
            bounds.X + caret.X,
            flipped ? bounds.Y + caret.Y : bounds.Y + bounds.Height - caret.Y - caret.Height,
            caret.Width,
            caret.Height);
        var inWindow = ObjC.GetRect(_view, "convertRect:toView:", local, 0);
        var window = ObjC.Send(_view, "window");
        return window == 0 ? inWindow : ObjC.GetRect(window, "convertRectToScreen:", inWindow);
    }

    // Function keys arrive as private-use characters U+F700–U+F7FF; GLFW drops them too.
    internal static string Filter(string text)
    {
        if (text.AsSpan().IndexOfAnyInRange('\uF700', '\uF7FF') < 0)
        {
            return text;
        }
        var kept = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is < '\uF700' or > '\uF7FF')
            {
                kept.Append(c);
            }
        }
        return kept.ToString();
    }

    /// <summary>Hands text input back to GLFW.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        Focus(null);
        _disposed = true;
        MacContentView.Detach(_view);
    }
}
