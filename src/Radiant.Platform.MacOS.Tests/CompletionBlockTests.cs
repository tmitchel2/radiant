using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Platform.MacOS.Tests;

[TestClass]
public unsafe class CompletionBlockTests
{
    private static readonly nint s_libSystem = NativeLibrary.Load("/usr/lib/libSystem.B.dylib");

    [TestMethod]
    public void ACopiedBlockOutlivesTheOriginalAndRunsTheActionOnce()
    {
        MacOnly.Require();
        var calls = 0;
        nint received = 0;
        nint copy;
        using (var block = new CompletionBlock(result =>
        {
            calls++;
            received = result;
        }))
        {
            // What AppKit does with a completion handler it keeps.
            copy = BlockCopy(block.Pointer);
        }
        Assert.AreNotEqual(0, copy);

        Invoke(copy, 42);
        Invoke(copy, 7);
        BlockRelease(copy);

        Assert.AreEqual(1, calls);
        Assert.AreEqual(42, received);
    }

    // A block's function pointer follows its isa, flags and reserved fields.
    private static void Invoke(nint block, nint value) =>
        ((delegate* unmanaged<nint, nint, void>)*(nint*)(block + 16))(block, value);

    private static nint BlockCopy(nint block) =>
        ((delegate* unmanaged<nint, nint>)NativeLibrary.GetExport(s_libSystem, "_Block_copy"))(block);

    private static void BlockRelease(nint block) =>
        ((delegate* unmanaged<nint, void>)NativeLibrary.GetExport(s_libSystem, "_Block_release"))(block);
}
