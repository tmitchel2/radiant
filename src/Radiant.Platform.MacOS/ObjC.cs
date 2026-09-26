using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace Radiant.Platform.MacOS;

/// <summary>
/// The Objective-C runtime, called directly: classes, selectors, messages, strings and runtime
/// class changes, with no binding library.
/// <para>
/// Messages go through <c>objc_msgSend</c> cast to a C function pointer of the exact signature
/// (<c>delegate* unmanaged&lt;…&gt;</c>), which is how the runtime expects to be called, costs
/// nothing to marshal and is safe under NativeAOT. Names cross as UTF-8 <c>byte*</c> and text as
/// UTF-16 <c>unichar*</c>, so nothing relies on string marshalling (CA2101).
/// </para>
/// <para>
/// This is the fuller sibling of <c>Radiant.Host.MacObjc</c>; see the improvements log for
/// merging the two.
/// </para>
/// </summary>
internal static unsafe class ObjC
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";
    private const string AppKitPath = "/System/Library/Frameworks/AppKit.framework/AppKit";

    private static readonly ConcurrentDictionary<string, nint> s_selectors = new();
    private static readonly ConcurrentDictionary<string, nint> s_classes = new();
    private static readonly nint s_appKit;

    static ObjC()
    {
        var objc = NativeLibrary.Load(LibObjC);
        MsgSend = NativeLibrary.GetExport(objc, "objc_msgSend");
        // x86-64 returns structs over 16 bytes through memory, which needs the _stret entry point;
        // arm64 has only objc_msgSend.
        MsgSendStret = RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? NativeLibrary.GetExport(objc, "objc_msgSend_stret")
            : MsgSend;
        // AppKit is loaded by GLFW in an app, but not in tests or tools: load it so its classes
        // and constants resolve either way.
        s_appKit = NativeLibrary.Load(AppKitPath);
    }

    /// <summary><c>objc_msgSend</c>, to be cast to the message's exact C signature.</summary>
    public static nint MsgSend { get; }

    /// <summary>The entry point for messages returning large structs such as <c>NSRect</c>.</summary>
    public static nint MsgSendStret { get; }

    /// <summary>A class by name, such as <c>NSPasteboard</c>; zero if there's none.</summary>
    public static nint Class(string name) => s_classes.GetOrAdd(name, static n =>
    {
        fixed (byte* p = Utf8(n))
        {
            return objc_getClass(p);
        }
    });

    /// <summary>A selector by name, such as <c>setString:forType:</c>.</summary>
    public static nint Sel(string name) => s_selectors.GetOrAdd(name, static n =>
    {
        fixed (byte* p = Utf8(n))
        {
            return sel_registerName(p);
        }
    });

    /// <summary>
    /// The value of an AppKit global such as <c>NSPasteboardTypeString</c>: most AppKit string
    /// constants are <c>NSString*</c> variables, so this reads the pointer stored at the symbol.
    /// </summary>
    public static nint AppKitConstant(string symbol) => *(nint*)NativeLibrary.GetExport(s_appKit, symbol);

    // ------------------------------------------------------------------ messages

    /// <summary><c>[receiver selector]</c> returning an object or integer.</summary>
    public static nint Send(nint receiver, string selector) =>
        ((delegate* unmanaged<nint, nint, nint>)MsgSend)(receiver, Sel(selector));

    /// <summary><c>[receiver selector:arg]</c> returning an object or integer.</summary>
    public static nint Send(nint receiver, string selector, nint arg) =>
        ((delegate* unmanaged<nint, nint, nint, nint>)MsgSend)(receiver, Sel(selector), arg);

    /// <summary><c>[receiver selector:a with:b]</c> returning an object or integer.</summary>
    public static nint Send(nint receiver, string selector, nint a, nint b) =>
        ((delegate* unmanaged<nint, nint, nint, nint, nint>)MsgSend)(receiver, Sel(selector), a, b);

    /// <summary><c>[receiver selector:a with:b with:c]</c> returning an object or integer.</summary>
    public static nint Send(nint receiver, string selector, nint a, nint b, nint c) =>
        ((delegate* unmanaged<nint, nint, nint, nint, nint, nint>)MsgSend)(receiver, Sel(selector), a, b, c);

    /// <summary><c>[receiver selector:a at:point in:b]</c> returning a <c>BOOL</c>, as <c>popUpMenuPositioningItem:atLocation:inView:</c>.</summary>
    public static bool GetBool(nint receiver, string selector, nint a, double x, double y, nint b) =>
        ((delegate* unmanaged<nint, nint, nint, double, double, nint, byte>)MsgSend)(receiver, Sel(selector), a, x, y, b) != 0;

    /// <summary><c>[receiver selector:a with:b with:c with:d]</c> returning an object or integer.</summary>
    public static nint Send(nint receiver, string selector, nint a, nint b, nint c, nint d) =>
        ((delegate* unmanaged<nint, nint, nint, nint, nint, nint, nint>)MsgSend)(receiver, Sel(selector), a, b, c, d);

    /// <summary><c>[receiver selector:flag]</c> with a <c>BOOL</c> argument.</summary>
    public static void SendBool(nint receiver, string selector, bool arg) =>
        ((delegate* unmanaged<nint, nint, byte, void>)MsgSend)(receiver, Sel(selector), arg ? (byte)1 : (byte)0);

    /// <summary><c>[receiver selector:rect]</c>, as <c>setAccessibilityFrameInParentSpace:</c>.</summary>
    public static void SendRect(nint receiver, string selector, NSRect rect) =>
        ((delegate* unmanaged<nint, nint, NSRect, void>)MsgSend)(receiver, Sel(selector), rect);

    /// <summary>
    /// Tells assistive technology about <paramref name="element"/> with AppKit's
    /// <c>NSAccessibilityPostNotification</c>, the notification named by an AppKit constant
    /// (such as <c>NSAccessibilityLayoutChangedNotification</c>).
    /// </summary>
    public static void PostAccessibilityNotification(nint element, string notification) =>
        ((delegate* unmanaged<nint, nint, void>)NativeLibrary.GetExport(s_appKit, "NSAccessibilityPostNotification"))(element, AppKitConstant(notification));

    /// <summary><c>[receiver selector:rect with:flag]</c>, as <c>setFrame:display:</c>.</summary>
    public static void SendRectBool(nint receiver, string selector, NSRect rect, bool flag) =>
        ((delegate* unmanaged<nint, nint, NSRect, byte, void>)MsgSend)(receiver, Sel(selector), rect, flag ? (byte)1 : (byte)0);

    /// <summary><c>[receiver selector]</c> returning a <c>BOOL</c>.</summary>
    public static bool GetBool(nint receiver, string selector) =>
        ((delegate* unmanaged<nint, nint, byte>)MsgSend)(receiver, Sel(selector)) != 0;

    /// <summary><c>[receiver selector:arg]</c> returning a <c>BOOL</c>.</summary>
    public static bool GetBool(nint receiver, string selector, nint arg) =>
        ((delegate* unmanaged<nint, nint, nint, byte>)MsgSend)(receiver, Sel(selector), arg) != 0;

    /// <summary><c>[receiver selector]</c> returning an <c>NSRect</c>.</summary>
    public static NSRect GetRect(nint receiver, string selector) =>
        ((delegate* unmanaged<nint, nint, NSRect>)MsgSendStret)(receiver, Sel(selector));

    /// <summary><c>[receiver selector:rect with:arg]</c> returning an <c>NSRect</c>, as <c>convertRect:toView:</c>.</summary>
    public static NSRect GetRect(nint receiver, string selector, NSRect rect, nint arg) =>
        ((delegate* unmanaged<nint, nint, NSRect, nint, NSRect>)MsgSendStret)(receiver, Sel(selector), rect, arg);

    /// <summary><c>[receiver selector:rect]</c> returning an <c>NSRect</c>, as <c>convertRectToScreen:</c>.</summary>
    public static NSRect GetRect(nint receiver, string selector, NSRect rect) =>
        ((delegate* unmanaged<nint, nint, NSRect, NSRect>)MsgSendStret)(receiver, Sel(selector), rect);

    /// <summary>Whether <paramref name="obj"/> is an instance of the class <paramref name="className"/> or a subclass.</summary>
    public static bool IsKindOf(nint obj, string className) =>
        obj != 0 && GetBool(obj, "isKindOfClass:", Class(className));

    /// <summary>Whether <paramref name="obj"/> responds to <paramref name="selector"/>, for APIs newer than the oldest supported macOS.</summary>
    public static bool RespondsTo(nint obj, string selector) =>
        obj != 0 && GetBool(obj, "respondsToSelector:", Sel(selector));

    // ------------------------------------------------------------------ strings and collections

    /// <summary>An autoreleased <c>NSString</c> holding <paramref name="value"/> exactly (UTF-16 in, UTF-16 kept).</summary>
    public static nint String(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        fixed (char* p = value)
        {
            return ((delegate* unmanaged<nint, nint, char*, nuint, nint>)MsgSend)(
                Class("NSString"), Sel("stringWithCharacters:length:"), p, (nuint)value.Length);
        }
    }

    /// <summary>
    /// The text of an <c>NSString</c>, or of an <c>NSAttributedString</c>'s string; null for nil.
    /// Read as UTF-16 code units, so indices into it match AppKit's ranges.
    /// </summary>
    public static string? ToManagedString(nint nsString)
    {
        if (nsString == 0)
        {
            return null;
        }
        if (IsKindOf(nsString, "NSAttributedString"))
        {
            nsString = Send(nsString, "string");
        }
        var length = (int)Send(nsString, "length");
        if (length == 0)
        {
            return "";
        }
        var buffer = new char[length];
        fixed (char* p = buffer)
        {
            ((delegate* unmanaged<nint, nint, char*, NSRange, void>)MsgSend)(
                nsString, Sel("getCharacters:range:"), p, new NSRange(0, (nuint)length));
        }
        return new string(buffer);
    }

    /// <summary>An autoreleased <c>NSArray</c> of <paramref name="items"/>.</summary>
    public static nint NSArray(params ReadOnlySpan<nint> items)
    {
        var array = Send(Class("NSMutableArray"), "arrayWithCapacity:", items.Length);
        foreach (var item in items)
        {
            Send(array, "addObject:", item);
        }
        return array;
    }

    // ------------------------------------------------------------------ memory

    /// <summary>
    /// Opens an autorelease pool for the scope: <c>using var pool = ObjC.Pool();</c>. Calls made
    /// from the UI thread outside AppKit's event dispatch (in a frame callback, a test) have no
    /// pool, and autoreleased objects would otherwise leak.
    /// </summary>
    public static AutoreleasePool Pool() => new(objc_autoreleasePoolPush());

    /// <summary>A scope that drains its autorelease pool when disposed.</summary>
    public readonly ref struct AutoreleasePool(nint token)
    {
        /// <summary>Drains the pool.</summary>
        public void Dispose() => objc_autoreleasePoolPop(token);
    }

    // ------------------------------------------------------------------ runtime classes

    /// <summary>The runtime class of an object.</summary>
    public static nint ClassOf(nint obj) => object_getClass(obj);

    /// <summary>The name of an object's class, for diagnostics.</summary>
    public static string ClassName(nint obj) => Marshal.PtrToStringUTF8(class_getName(object_getClass(obj))) ?? "";

    /// <summary>
    /// Replaces a method's implementation on <paramref name="cls"/> (adding it if the class
    /// doesn't define it) and returns the implementation it had, zero if none.
    /// </summary>
    public static nint ReplaceMethod(nint cls, string selector, nint implementation, string types)
    {
        var sel = Sel(selector);
        var existing = class_getInstanceMethod(cls, sel);
        var previous = existing == 0 ? 0 : method_getImplementation(existing);
        fixed (byte* t = Utf8(types))
        {
            class_replaceMethod(cls, sel, implementation, t);
        }
        return previous;
    }

    /// <summary>
    /// Defines a new subclass of <c>NSObject</c> with the given methods, or returns the existing
    /// class if this process already defined it (classes can't be unregistered).
    /// </summary>
    public static nint DefineClass(string name, params ReadOnlySpan<(string Selector, nint Implementation, string Types)> methods) =>
        DefineSubclass(name, "NSObject", methods);

    /// <summary>Defines a new subclass of <paramref name="superclass"/>, as <see cref="DefineClass"/> does of <c>NSObject</c>.</summary>
    public static nint DefineSubclass(string name, string superclass, params ReadOnlySpan<(string Selector, nint Implementation, string Types)> methods)
    {
        if (Class(name) is var existing and not 0)
        {
            return existing;
        }
        nint cls;
        fixed (byte* n = Utf8(name))
        {
            cls = objc_allocateClassPair(Class(superclass), n, 0);
        }
        foreach (var (selector, implementation, types) in methods)
        {
            fixed (byte* t = Utf8(types))
            {
                class_addMethod(cls, Sel(selector), implementation, t);
            }
        }
        objc_registerClassPair(cls);
        s_classes[name] = cls;
        return cls;
    }

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value + "\0");

    [DllImport(LibObjC)]
    private static extern nint objc_getClass(byte* name);

    [DllImport(LibObjC)]
    private static extern nint sel_registerName(byte* name);

    [DllImport(LibObjC)]
    private static extern nint object_getClass(nint obj);

    [DllImport(LibObjC)]
    private static extern nint class_getName(nint cls);

    [DllImport(LibObjC)]
    private static extern nint class_getInstanceMethod(nint cls, nint sel);

    [DllImport(LibObjC)]
    private static extern nint method_getImplementation(nint method);

    [DllImport(LibObjC)]
    private static extern nint class_replaceMethod(nint cls, nint sel, nint imp, byte* types);

    [DllImport(LibObjC)]
    private static extern byte class_addMethod(nint cls, nint sel, nint imp, byte* types);

    [DllImport(LibObjC)]
    private static extern nint objc_allocateClassPair(nint superclass, byte* name, nuint extraBytes);

    [DllImport(LibObjC)]
    private static extern void objc_registerClassPair(nint cls);

    [DllImport(LibObjC)]
    private static extern nint objc_autoreleasePoolPush();

    [DllImport(LibObjC)]
    private static extern void objc_autoreleasePoolPop(nint token);
}
