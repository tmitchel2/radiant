using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Platform.MacOS.Tests;

/// <summary>
/// Runs <c>PlatformCheck --selftest</c>, which opens a window and checks text input and cursors
/// against GLFW's real content view. It needs the main thread (AppKit windows can't be made on a
/// test thread), hence a process of its own, and a window, hence Integration.
/// </summary>
[TestClass]
public class SelfTestTests
{
    [TestMethod]
    [TestCategory(MacOnly.Integration)]
    public void TextInputWorksOnGlfwsRealView()
    {
        MacOnly.Require();
        // Built next to this project: …/src/Radiant.Platform.MacOS.Tests/bin/<config>/<tfm>/.
        var testOutput = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var tfm = testOutput.Name;
        var configuration = testOutput.Parent!.Name;
        var src = testOutput.Parent.Parent!.Parent!.Parent!.FullName;
        var check = Path.Combine(src, "PlatformCheck", "bin", configuration, tfm, "PlatformCheck.dll");
        if (!File.Exists(check))
        {
            Assert.Inconclusive($"Build PlatformCheck first: {check} is missing.");
        }

        using var process = Process.Start(new ProcessStartInfo("dotnet", $"\"{check}\" --selftest")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(60)))
        {
            process.Kill();
            Assert.Fail("The self-test didn't finish within a minute.");
        }
        Console.WriteLine(output.Result);
        Console.WriteLine(errors.Result);

        Assert.AreEqual(0, process.ExitCode, output.Result);
        StringAssert.Contains(output.Result, "All checks passed.");
    }
}
