using System.Runtime.InteropServices;
using System.Text;

namespace Radiant.Host;

/// <summary>
/// Thin Objective-C runtime interop shared by the macOS Dock helpers (<see cref="MacDockMenu"/>,
/// <see cref="MacWindowFocus"/>). Calls route through <c>libobjc</c> via P/Invoke; strings cross as UTF-8
/// <c>byte*</c> (no string marshalling) to satisfy CA2101, matching <see cref="MacDockIcon"/>'s template.
///
/// <para>All members are no-ops to call only when <see cref="IsMac"/>; callers guard first. Selector and
/// class lookups copy their UTF-8 name into a pinned buffer that is valid only for the duration of the
/// native call — that is sufficient because <c>objc_getClass</c>/<c>sel_registerName</c>/<c>class_addMethod</c>
/// each copy or intern what they need synchronously.</para>
/// </summary>
internal static unsafe class MacObjc
{
    private const string Lib = "/usr/lib/libobjc.A.dylib";

    /// <summary>Whether the current OS is macOS (where any of these calls are meaningful).</summary>
    public static bool IsMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>Look up an Objective-C class by name (e.g. <c>NSMenu</c>).</summary>
    public static IntPtr Cls(string name)
    {
        var utf8 = Encoding.UTF8.GetBytes(name + "\0");
        fixed (byte* p = utf8) return objc_getClass((IntPtr)p);
    }

    /// <summary>Register/look up a selector by name (e.g. <c>addItem:</c>).</summary>
    public static IntPtr Sel(string name)
    {
        var utf8 = Encoding.UTF8.GetBytes(name + "\0");
        fixed (byte* p = utf8) return sel_registerName((IntPtr)p);
    }

    /// <summary><c>[NSApplication sharedApplication]</c>.</summary>
    public static IntPtr SharedApp() => Send(Cls("NSApplication"), Sel("sharedApplication"));

    /// <summary>The runtime class of an instance (<c>object_getClass</c>).</summary>
    public static IntPtr GetClass(IntPtr obj) => object_getClass(obj);

    /// <summary>Add a method to a class, returning whether it was added (false if it already exists).</summary>
    public static bool AddMethod(IntPtr cls, IntPtr sel, IntPtr imp, string typeEncoding)
    {
        var utf8 = Encoding.UTF8.GetBytes(typeEncoding + "\0");
        fixed (byte* p = utf8) return class_addMethod(cls, sel, imp, (IntPtr)p) != 0;
    }

    /// <summary>An autoreleased <c>NSString</c> from a managed string (<c>stringWithUTF8String:</c>).</summary>
    public static IntPtr NSString(string value)
    {
        var utf8 = Encoding.UTF8.GetBytes(value + "\0");
        fixed (byte* p = utf8) return objc_msgSend_ptr(Cls("NSString"), Sel("stringWithUTF8String:"), (IntPtr)p);
    }

    /// <summary>Read a C string returned by a selector (e.g. <c>UTF8String</c>) back into a managed string.</summary>
    public static string? ReadUtf8(IntPtr cString) => Marshal.PtrToStringUTF8(cString);

    // objc_msgSend overloads — one per native arg shape we use. All share the libobjc entry point; the
    // managed signatures select the right calling convention for the argument list.
    public static IntPtr Send(IntPtr receiver, IntPtr selector) => objc_msgSend(receiver, selector);

    public static IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg) =>
        objc_msgSend_ptr(receiver, selector, arg);

    /// <summary>Send with a single Objective-C <c>BOOL</c> argument (a signed char), e.g. <c>setCanChooseFiles:</c>.</summary>
    public static IntPtr Send(IntPtr receiver, IntPtr selector, bool arg) =>
        objc_msgSend_b(receiver, selector, arg ? (byte)1 : (byte)0);

    public static IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a, IntPtr b, IntPtr c) =>
        objc_msgSend_ppp(receiver, selector, a, b, c);

    [DllImport(Lib)]
    private static extern IntPtr objc_getClass(IntPtr name);

    [DllImport(Lib)]
    private static extern IntPtr sel_registerName(IntPtr name);

    [DllImport(Lib)]
    private static extern IntPtr object_getClass(IntPtr obj);

    [DllImport(Lib)]
    private static extern byte class_addMethod(IntPtr cls, IntPtr name, IntPtr imp, IntPtr types);

    [DllImport(Lib)]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ptr(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_b(IntPtr receiver, IntPtr selector, byte arg);

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ppp(IntPtr receiver, IntPtr selector, IntPtr a, IntPtr b, IntPtr c);
}
