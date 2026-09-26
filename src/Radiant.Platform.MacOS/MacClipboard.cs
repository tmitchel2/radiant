using System;

namespace Radiant.Platform.MacOS;

/// <summary>
/// Text on an <c>NSPasteboard</c>: the general pasteboard (the system clipboard) in an app, or a
/// private, uniquely named one in tests so they don't overwrite what the user copied.
/// </summary>
internal sealed class MacClipboard : IClipboard, IDisposable
{
    private readonly nint _pasteboard;
    private readonly bool _owned;
    private bool _disposed;

    private MacClipboard(nint pasteboard, bool owned)
    {
        _pasteboard = pasteboard;
        _owned = owned;
    }

    /// <summary>The system clipboard.</summary>
    public static MacClipboard General()
    {
        using var pool = ObjC.Pool();
        return new MacClipboard(ObjC.Send(ObjC.Send(ObjC.Class("NSPasteboard"), "generalPasteboard"), "retain"), owned: false);
    }

    /// <summary>A new pasteboard only this object uses, released from the system when disposed.</summary>
    public static MacClipboard Private()
    {
        using var pool = ObjC.Pool();
        return new MacClipboard(ObjC.Send(ObjC.Send(ObjC.Class("NSPasteboard"), "pasteboardWithUniqueName"), "retain"), owned: true);
    }

    // NSPasteboardTypeString is "public.utf8-plain-text"; read it from AppKit rather than assume.
    private static nint StringType => ObjC.AppKitConstant("NSPasteboardTypeString");

    /// <inheritdoc/>
    public bool HasText
    {
        get
        {
            using var pool = ObjC.Pool();
            return ObjC.Send(_pasteboard, "availableTypeFromArray:", ObjC.NSArray(StringType)) != 0;
        }
    }

    /// <inheritdoc/>
    public string? GetText()
    {
        using var pool = ObjC.Pool();
        return ObjC.ToManagedString(ObjC.Send(_pasteboard, "stringForType:", StringType));
    }

    /// <inheritdoc/>
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        using var pool = ObjC.Pool();
        // Writing needs the pasteboard's ownership first: clearContents takes it.
        ObjC.Send(_pasteboard, "clearContents");
        ObjC.Send(_pasteboard, "setString:forType:", ObjC.String(text), StringType);
    }

    /// <summary>Releases the pasteboard, and removes a private one from the system.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_owned)
        {
            ObjC.Send(_pasteboard, "releaseGlobally");
        }
        ObjC.Send(_pasteboard, "release");
    }
}
