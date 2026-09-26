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

/// <summary>
/// The Unicode Bidirectional Algorithm against Unicode's own conformance data: BidiTest.txt
/// (every sequence of up to four Bidi_Class values, and longer pitfalls, at each paragraph
/// direction) and BidiCharacterTest.txt (real text, including bracket pairs). Every case must pass.
/// </summary>
[TestClass]
public class BidiConformanceTests
{
    [TestMethod]
    public void BidiTestPassesEveryCase()
    {
        int[] expectedLevels = [];
        int[] expectedOrder = [];
        var cases = 0;
        var failures = 0;
        string? first = null;

        foreach (var raw in ReadLines("BidiTest.txt.gz"))
        {
            var line = raw.Split('#')[0].Trim();
            if (line.Length == 0)
            {
                continue;
            }
            if (line.StartsWith("@Levels:", StringComparison.Ordinal))
            {
                expectedLevels = [.. Tokens(line["@Levels:".Length..]).Select(t => t == "x" ? -1 : int.Parse(t, CultureInfo.InvariantCulture))];
                continue;
            }
            if (line.StartsWith("@Reorder:", StringComparison.Ordinal))
            {
                expectedOrder = [.. Tokens(line["@Reorder:".Length..]).Select(t => int.Parse(t, CultureInfo.InvariantCulture))];
                continue;
            }
            if (line.StartsWith('@'))
            {
                continue;
            }

            var fields = line.Split(';');
            var classes = Tokens(fields[0]).Select(Enum.Parse<BidiClass>).ToArray();
            var bitset = int.Parse(fields[1].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            foreach (var (bit, direction) in new (int, TextDirection?)[] { (1, null), (2, TextDirection.LeftToRight), (4, TextDirection.RightToLeft) })
            {
                if ((bitset & bit) == 0)
                {
                    continue;
                }
                cases++;
                var paragraph = BidiParagraph.Resolve(classes, direction);
                var levels = LevelsOf(paragraph, classes);
                var order = OrderOf(paragraph, classes);
                if (!levels.SequenceEqual(expectedLevels) || !order.SequenceEqual(expectedOrder))
                {
                    failures++;
                    first ??= $"{fields[0].Trim()} at {direction?.ToString() ?? "auto"}: levels [{Show(levels)}] order [{string.Join(' ', order)}], "
                        + $"expected [{Show(expectedLevels)}] [{string.Join(' ', expectedOrder)}]";
                }
            }
        }

        Assert.IsTrue(cases > 700_000, $"only {cases} cases read");
        Assert.AreEqual(0, failures, $"{failures} of {cases} differ; first: {first}");
    }

    [TestMethod]
    public void BidiCharacterTestPassesEveryCase()
    {
        var cases = 0;
        var failures = 0;
        string? first = null;

        foreach (var raw in ReadLines("BidiCharacterTest.txt.gz"))
        {
            var line = raw.Split('#')[0].Trim();
            if (line.Length == 0)
            {
                continue;
            }
            var fields = line.Split(';');
            var codePoints = Tokens(fields[0]).Select(t => int.Parse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
            TextDirection? direction = fields[1].Trim() switch
            {
                "0" => TextDirection.LeftToRight,
                "1" => TextDirection.RightToLeft,
                _ => null,
            };
            var expectedParagraphLevel = int.Parse(fields[2].Trim(), CultureInfo.InvariantCulture);
            var expectedLevels = Tokens(fields[3]).Select(t => t == "x" ? -1 : int.Parse(t, CultureInfo.InvariantCulture)).ToArray();
            var expectedOrder = Tokens(fields[4]).Select(t => int.Parse(t, CultureInfo.InvariantCulture)).ToArray();

            // The test indexes code points; the paragraph indexes UTF-16 code units.
            var text = new StringBuilder();
            var starts = new int[codePoints.Length];
            for (var k = 0; k < codePoints.Length; k++)
            {
                starts[k] = text.Length;
                text.Append(char.ConvertFromUtf32(codePoints[k]));
            }
            var paragraph = BidiParagraph.Resolve(text.ToString(), direction);
            var classes = codePoints.Select(BidiClassData.Get).ToArray();

            var lineLevels = paragraph.GetLineLevels(0, text.Length);
            var levels = new int[codePoints.Length];
            for (var k = 0; k < codePoints.Length; k++)
            {
                levels[k] = BidiResolver.IsRemovedByX9(classes[k]) ? -1 : lineLevels[starts[k]];
            }
            var codePointOf = new Dictionary<int, int>();
            for (var k = 0; k < codePoints.Length; k++)
            {
                codePointOf[starts[k]] = k;
            }
            var order = paragraph.GetVisualOrder(0, text.Length)
                .Where(codePointOf.ContainsKey)
                .Select(i => codePointOf[i])
                .Where(k => !BidiResolver.IsRemovedByX9(classes[k]))
                .ToArray();

            cases++;
            if (paragraph.BaseLevel != expectedParagraphLevel || !levels.SequenceEqual(expectedLevels) || !order.SequenceEqual(expectedOrder))
            {
                failures++;
                first ??= $"{fields[0].Trim()} at {direction?.ToString() ?? "auto"}: paragraph {paragraph.BaseLevel} levels [{Show(levels)}] "
                    + $"order [{string.Join(' ', order)}], expected {expectedParagraphLevel} [{Show(expectedLevels)}] [{string.Join(' ', expectedOrder)}]";
            }
        }

        Assert.IsTrue(cases > 90_000, $"only {cases} cases read");
        Assert.AreEqual(0, failures, $"{failures} of {cases} differ; first: {first}");
    }

    /// <summary>Line levels with X9-removed characters as -1, which the test data writes as x.</summary>
    private static int[] LevelsOf(BidiParagraph paragraph, BidiClass[] classes)
    {
        var levels = paragraph.GetLineLevels(0, classes.Length);
        return [.. levels.Select((level, i) => BidiResolver.IsRemovedByX9(classes[i]) ? -1 : level)];
    }

    /// <summary>The visual order without X9-removed characters, which the test data leaves out.</summary>
    private static int[] OrderOf(BidiParagraph paragraph, BidiClass[] classes) =>
        [.. paragraph.GetVisualOrder(0, classes.Length).Where(i => !BidiResolver.IsRemovedByX9(classes[i]))];

    private static string Show(int[] levels) => string.Join(' ', levels.Select(l => l < 0 ? "x" : l.ToString(CultureInfo.InvariantCulture)));

    private static string[] Tokens(string s) => s.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

    private static IEnumerable<string> ReadLines(string file)
    {
        using var stream = new GZipStream(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "UnicodeData", file)), CompressionMode.Decompress);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}
