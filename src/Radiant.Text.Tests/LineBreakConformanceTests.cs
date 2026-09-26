using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Text.Unicode;

namespace Radiant.Text.Tests;

[TestClass]
public class LineBreakConformanceTests
{
    [TestMethod]
    public void EveryLineOfTheUnicodeTestBreaksWhereItShould()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "UnicodeData", "LineBreakTest.txt.gz");
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);

        var total = 0;
        var failures = 0;
        string? first = null;
        while (reader.ReadLine() is { } line)
        {
            var hash = line.IndexOf('#', StringComparison.Ordinal);
            var body = (hash < 0 ? line : line[..hash]).Trim();
            if (body.Length == 0)
            {
                continue;
            }

            var text = new StringBuilder();
            var expected = new List<int>();
            foreach (var token in body.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (token == "÷")
                {
                    if (text.Length > 0)
                    {
                        expected.Add(text.Length);
                    }
                }
                else if (token != "×")
                {
                    text.Append(char.ConvertFromUtf32(int.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
                }
            }

            total++;
            var actual = LineBreaker.GetOpportunities(text.ToString()).Select(b => b.Index).ToList();
            if (!actual.SequenceEqual(expected))
            {
                failures++;
                first ??= $"{line}\n  expected breaks at [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]";
            }
        }

        Assert.IsTrue(total > 10000, $"Only {total} test lines read");
        Assert.AreEqual(0, failures, $"{failures} of {total} lines differ; first: {first}");
    }
}
