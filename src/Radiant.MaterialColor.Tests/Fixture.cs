using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.MaterialColor.Tests;

/// <summary>
/// Loads the outputs upstream material-color-utilities produced for the same inputs
/// (tools/mcu-fixtures/generate.sh), which the port must reproduce.
/// </summary>
internal static class Fixture
{
    /// <summary>The parsed fixture <c>Fixtures/&lt;name&gt;.json.gz</c>.</summary>
    public static JsonElement Load(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".json.gz");
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var document = JsonDocument.Parse(gzip);
        return document.RootElement.Clone();
    }

    /// <summary>A colour as upstream writes it (an unsigned 0xAARRGGBB number), as this port holds it.</summary>
    public static int Argb(JsonElement value) => unchecked((int)value.GetUInt32());

    /// <summary>A colour for a failure message.</summary>
    public static string Hex(int argb) => $"0x{argb:X8}";

    /// <summary>
    /// Asserts two doubles agree to within a relative 1e-9. Upstream runs on V8, whose pow, cbrt and
    /// trigonometric functions can differ from .NET's in the last bit; colours, which round these to
    /// integers, are compared exactly.
    /// </summary>
    public static void AreClose(double expected, double actual, string message)
    {
        Assert.IsTrue(IsClose(expected, actual), $"{message}: expected {expected:R}, got {actual:R}");
    }

    /// <summary>Whether two doubles agree to within the tolerance of <see cref="AreClose"/>.</summary>
    public static bool IsClose(double expected, double actual)
    {
        var tolerance = 1e-9 * Math.Max(1.0, Math.Abs(expected));
        return Math.Abs(expected - actual) <= tolerance;
    }
}
