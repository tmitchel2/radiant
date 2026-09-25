namespace Radiant.Host;

/// <summary>
/// Entry point for the floating drag-overlay process, launched as <c>&lt;app&gt; --type drag-overlay</c>.
/// <c>--selftest</c> runs the headless draw-path smoke check instead of opening the live window.
/// </summary>
public static class DragOverlayEntry
{
    /// <summary>Runs the drag overlay as <paramref name="identity"/>, which it installs for this process.</summary>
    public static int Run(string[] args, RadiantAppIdentity identity)
    {
        RadiantAppIdentity.Use(identity);

        if (args.Contains("--selftest"))
        {
            var outPng = GetArg(args, "--out") ?? Path.Combine(Path.GetTempPath(), "radiant-drag-overlay-selftest.png");
            return DragOverlaySelfTest.Run(outPng);
        }

        using var overlay = new DragOverlay();
        overlay.Run();
        return 0;
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
