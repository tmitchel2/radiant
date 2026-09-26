using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.MaterialColor.Tests;

/// <summary>Tonal palettes and their key colours against upstream's outputs.</summary>
[TestClass]
public class PaletteFixtureTests
{
    [TestMethod]
    public void PalettesFromColoursMatchUpstream()
    {
        var fixture = Fixture.Load("palettes");
        var tones = Tones(fixture);
        var mismatches = new Mismatches();
        foreach (var row in fixture.GetProperty("palettes").EnumerateArray())
        {
            var argb = Fixture.Argb(row.GetProperty("argb"));
            var palette = TonalPalette.FromInt(argb);
            var label = Fixture.Hex(argb);
            mismatches.Double(row.GetProperty("hue").GetDouble(), palette.Hue, label + " hue");
            mismatches.Double(row.GetProperty("chroma").GetDouble(), palette.Chroma, label + " chroma");
            mismatches.Colour(Fixture.Argb(row.GetProperty("keyColor")), palette.KeyColor.ToInt(), label + " key colour");
            CompareTones(mismatches, tones, row, palette, label);
        }
        mismatches.AssertNone();
    }

    [TestMethod]
    public void PalettesFromHueAndChromaFindTheSameKeyColoursAndTonesAsUpstream()
    {
        var fixture = Fixture.Load("palettes");
        var tones = Tones(fixture);
        var mismatches = new Mismatches();
        foreach (var row in fixture.GetProperty("hueChroma").EnumerateArray())
        {
            var hue = row.GetProperty("hue").GetDouble();
            var chroma = row.GetProperty("chroma").GetDouble();
            var palette = TonalPalette.FromHueAndChroma(hue, chroma);
            var label = $"H{hue} C{chroma}";
            mismatches.Colour(Fixture.Argb(row.GetProperty("keyColor")), palette.KeyColor.ToInt(), label + " key colour");
            CompareTones(mismatches, tones, row, palette, label);
        }
        mismatches.AssertNone();
    }

    private static double[] Tones(JsonElement fixture) =>
        [.. fixture.GetProperty("tones").EnumerateArray().Select(t => t.GetDouble())];

    private static void CompareTones(Mismatches mismatches, double[] tones, JsonElement row, TonalPalette palette, string label)
    {
        var expected = row.GetProperty("tones").EnumerateArray().Select(Fixture.Argb).ToArray();
        for (var i = 0; i < tones.Length; i++)
        {
            mismatches.Colour(expected[i], palette.Tone(tones[i]), $"{label} T{tones[i]}");
        }
    }
}
