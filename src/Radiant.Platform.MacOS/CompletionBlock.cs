using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Radiant.Platform.MacOS;

/// <summary>
/// An Objective-C block of type <c>void (^)(NSInteger)</c> that calls a managed action, for
/// AppKit's completion handlers (<c>beginSheetModalForWindow:completionHandler:</c>).
/// <para>
/// A block is a struct the runtime can copy: <c>isa</c>, flags, the function to call and a
/// descriptor, followed by whatever it captured. This makes a <em>stack</em> block (in unmanaged
/// memory) capturing a <see cref="GCHandle"/> to the action. The method it's passed to copies it
/// to the heap with <c>Block_copy</c> before returning, as it must to keep a handler it calls
/// later; that copy is AppKit's, and this one is freed by <see cref="Dispose"/> once the call
/// returns. The action runs once, and frees the handle.
/// </para>
/// </summary>
internal sealed unsafe class CompletionBlock : IDisposable
{
    private const int HasSignature = 1 << 30;

    private static readonly nint s_stackBlockClass = NativeLibrary.GetExport(
        NativeLibrary.Load("/usr/lib/libSystem.B.dylib"), "_NSConcreteStackBlock");

    // The descriptor and its type signature (void, block, NSInteger) are shared and live forever.
    private static readonly Descriptor* s_descriptor = CreateDescriptor();

    private Literal* _literal;

    /// <summary>A block that runs <paramref name="action"/> with the integer AppKit passes.</summary>
    public CompletionBlock(Action<nint> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _literal = (Literal*)NativeMemory.AllocZeroed((nuint)sizeof(Literal));
        _literal->Isa = s_stackBlockClass;
        _literal->Flags = HasSignature;
        _literal->Invoke = (nint)(delegate* unmanaged<Literal*, nint, void>)&Invoke;
        _literal->Descriptor = s_descriptor;
        _literal->Action = GCHandle.ToIntPtr(GCHandle.Alloc(action));
    }

    /// <summary>The block, to pass as an argument.</summary>
    public nint Pointer => (nint)_literal;

    /// <summary>Frees this (stack) copy; AppKit's heap copy is unaffected.</summary>
    public void Dispose()
    {
        if (_literal is not null)
        {
            NativeMemory.Free(_literal);
            _literal = null;
        }
    }

    [UnmanagedCallersOnly]
    private static void Invoke(Literal* block, nint result)
    {
#pragma warning disable CA1031 // An exception must not unwind into AppKit, which would abort the process.
        try
        {
            // The heap copy carries the handle; clear it so a second call does nothing.
            var handle = block->Action;
            block->Action = 0;
            if (handle == 0)
            {
                return;
            }
            var gcHandle = GCHandle.FromIntPtr(handle);
            var action = (Action<nint>)gcHandle.Target!;
            gcHandle.Free();
            action(result);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[platform] completion handler failed: {e}");
        }
#pragma warning restore CA1031
    }

    private static Descriptor* CreateDescriptor()
    {
        var descriptor = (Descriptor*)NativeMemory.AllocZeroed((nuint)sizeof(Descriptor));
        descriptor->Size = (nuint)sizeof(Literal);
        var signature = Encoding.ASCII.GetBytes("v16@?0q8\0");
        var copy = (byte*)NativeMemory.Alloc((nuint)signature.Length);
        signature.CopyTo(new Span<byte>(copy, signature.Length));
        descriptor->Signature = copy;
        return descriptor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Literal
    {
        public nint Isa;
        public int Flags;
        public int Reserved;
        public nint Invoke;
        public Descriptor* Descriptor;
        public nint Action;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Descriptor
    {
        public nuint Reserved;
        public nuint Size;
        public byte* Signature;
    }
}
