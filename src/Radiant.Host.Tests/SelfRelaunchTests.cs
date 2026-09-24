namespace Radiant.Host.Tests;

[TestClass]
public sealed class SelfRelaunchTests
{
    private static readonly string[] s_muxerExpected = ["/wt/App.dll", "--type", "host"];
    private static readonly string[] s_muxerWinExpected = ["/wt/App.dll", "--type", "drag-overlay"];
    private static readonly string[] s_hostOnly = ["--type", "host"];
    private static readonly string[] s_overlayOnly = ["--type", "drag-overlay"];
    private static readonly string[] s_appHostExpected = ["--type", "host", "--name", "h2"];

    // Launched via `dotnet run` / `dotnet App.dll`: the muxer needs the entry DLL prepended so it has a
    // program to run, then the trailing args.
    [TestMethod]
    public void MuxerLaunchPrependsEntryDll()
    {
        var args = SelfRelaunch.BuildArguments("/usr/local/share/dotnet/dotnet", "/wt/App.dll", "--type", "host");
        CollectionAssert.AreEqual(s_muxerExpected, args.ToArray());
    }

    // On Windows ProcessPath is `…\dotnet.exe`; basename comparison must be case- and extension-insensitive.
    [TestMethod]
    public void MuxerLaunchIsCaseAndExtensionInsensitive()
    {
        var args = SelfRelaunch.BuildArguments("/usr/local/share/dotnet/DOTNET.exe", "/wt/App.dll", "--type", "drag-overlay");
        CollectionAssert.AreEqual(s_muxerWinExpected, args.ToArray());
    }

    // Native AOT / single-file publish: Environment.ProcessPath is the native binary and the entry assembly
    // has no on-disk location. The binary IS the program — nothing is prepended. (Regression guard for the
    // "dotnet---type does not exist" bug, where an empty DLL was passed to a non-existent `dotnet`.)
    [TestMethod]
    public void AotLaunchPassesOnlyTrailingArgs()
    {
        var args = SelfRelaunch.BuildArguments("/Users/tom/publish/App", entryDll: "", "--type", "host");
        CollectionAssert.AreEqual(s_hostOnly, args.ToArray());
    }

    [TestMethod]
    public void AotLaunchWithNullEntryDllPassesOnlyTrailingArgs()
    {
        var args = SelfRelaunch.BuildArguments("/Users/tom/publish/App", entryDll: null, "--type", "drag-overlay");
        CollectionAssert.AreEqual(s_overlayOnly, args.ToArray());
    }

    // Framework-dependent apphost (`./App` launching `App.dll` next to it): ProcessPath is the apphost
    // shim, not the muxer, so the apphost itself is the program — the DLL must not be prepended.
    [TestMethod]
    public void AppHostLaunchDoesNotPrependDll()
    {
        var args = SelfRelaunch.BuildArguments("/wt/bin/App", "/wt/bin/App.dll", "--type", "host", "--name", "h2");
        CollectionAssert.AreEqual(s_appHostExpected, args.ToArray());
    }

    [TestMethod]
    public void NoTrailingArgsYieldsEmptyForAot()
    {
        var args = SelfRelaunch.BuildArguments("/Users/tom/publish/App", entryDll: null);
        Assert.AreEqual(0, args.Count);
    }
}
