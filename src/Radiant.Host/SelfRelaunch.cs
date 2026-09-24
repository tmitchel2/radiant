using System.Diagnostics;
using System.Reflection;

namespace Radiant.Host;

/// <summary>
/// Builds a <see cref="ProcessStartInfo"/> that re-launches the *current* application executable with the
/// given trailing arguments (e.g. <c>--type host</c> or <c>--type drag-overlay</c>) — used by every
/// multi-process spawn that brings up another instance of *this same binary* (the compositing host, a
/// torn-off host window, the floating drag-overlay).
///
/// <para><see cref="Environment.ProcessPath"/> is the binary the OS actually started: the native application
/// binary under a Native AOT / single-file publish, the apphost for a framework-dependent build, or the
/// <c>dotnet</c> muxer when launched via <c>dotnet run</c> / <c>dotnet MyApp.dll</c>. Only in that last
/// (muxer) case is the managed entry assembly a real DLL on disk that must be re-passed as the first
/// argument; under AOT/single-file <see cref="Assembly.GetEntryAssembly"/>'s <c>Location</c> is empty, so a
/// hard-coded <c>dotnet &lt;entryDll&gt;</c> spawn degraded to a bare <c>dotnet --type host</c> — and with
/// no <c>dotnet</c> muxer on PATH in a publish dir that failed at runtime with
/// "dotnet---type does not exist" (the muxer treating <c>--type</c> as a <c>dotnet---type</c> tool name).</para>
/// </summary>
public static class SelfRelaunch
{
    /// <summary>
    /// Create a <see cref="ProcessStartInfo"/> for the current executable, with <paramref name="trailingArgs"/>
    /// appended after the entry DLL (when launched via the <c>dotnet</c> muxer).
    /// </summary>
    public static ProcessStartInfo BuildStartInfo(params string[] trailingArgs)
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine executable path to relaunch the application.");
        var psi = new ProcessStartInfo(exePath) { UseShellExecute = false };
        foreach (var a in BuildArguments(exePath, Assembly.GetEntryAssembly()?.Location, trailingArgs))
        {
            psi.ArgumentList.Add(a);
        }
        return psi;
    }

    /// <summary>
    /// Pure argument-list builder behind <see cref="BuildStartInfo"/>: when <paramref name="exePath"/> is the
    /// <c>dotnet</c> muxer (and the managed entry assembly exists on disk) the entry DLL is prepended so the
    /// muxer has a program to run; for a native AOT / single-file / apphost binary nothing is prepended (the
    /// binary *is* the program) — prepending a (here empty) DLL was the bug that produced a bare
    /// <c>dotnet --type host</c> and the "dotnet---type does not exist" failure.
    /// </summary>
    public static IReadOnlyList<string> BuildArguments(string exePath, string? entryDll, params string[] trailingArgs)
    {
        var args = new List<string>();
        var launchedViaMuxer = string.Equals(
            Path.GetFileNameWithoutExtension(exePath), "dotnet", StringComparison.OrdinalIgnoreCase);
        if (launchedViaMuxer && !string.IsNullOrEmpty(entryDll))
        {
            args.Add(entryDll);
        }
        args.AddRange(trailingArgs);
        return args;
    }
}
