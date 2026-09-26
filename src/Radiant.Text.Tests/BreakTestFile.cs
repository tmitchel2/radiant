using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Radiant.Text.Tests;

/// <summary>
/// Checks a segmentation algorithm against one of Unicode's conformance files (gzipped
/// WordBreakTest.txt, GraphemeBreakTest.txt), where each line is a case such as
/// <c>÷ 0041 × 0308 ÷ 0020 ÷</c>: code points with a boundary (÷) or none (×) between them.
/// </summary>
internal static class BreakTestFile
{
    /// <summary>
    /// Runs every case through <paramref name="segment"/>, returning how many cases there are, how
    /// many the algorithm gets wrong, and the first it gets wrong.
    /// </summary>
    public static (int Total, int Failures, string? First) Check(string fileName, Func<string, IReadOnlyList<int>> segment)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "UnicodeData", fileName);
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);

        var total = 0;
        var failures = 0;
        string? first = null;
        while (reader.ReadLine() is { } line)
        {
            var hash = line.IndexOf('#', StringComparison.Ordinal);
            var body = (hash >= 0 ? line[..hash] : line).Trim();
            if (body.Length == 0)
            {
                continue;
            }
            total++;
            var text = new StringBuilder();
            var expected = new List<int>();
            foreach (var token in body.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token == "÷")
                {
                    expected.Add(text.Length);
                }
                else if (token != "×")
                {
                    text.Append(char.ConvertFromUtf32(int.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
                }
            }
            var actual = segment(text.ToString());
            if (!actual.SequenceEqual(expected))
            {
                failures++;
                first ??= $"{body} got [{string.Join(", ", actual)}], expected [{string.Join(", ", expected)}] ({line[(hash + 1)..].Trim()})";
            }
        }
        return (total, failures, first);
    }
}
