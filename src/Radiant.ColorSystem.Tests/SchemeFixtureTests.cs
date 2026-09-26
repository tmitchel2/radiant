using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.ColorSystem.Tests;

/// <summary>
/// Every color role of every scheme upstream generated — 16 source colors × 10 variants × light/dark
/// × 4 contrast levels × 3 spec versions, plus watch schemes and multi-source schemes — against
/// upstream's values.
/// </summary>
[TestClass]
public class SchemeFixtureTests
{
    private static readonly Dictionary<string, Variant> s_variants = new()
    {
        ["MONOCHROME"] = Variant.Monochrome,
        ["NEUTRAL"] = Variant.Neutral,
        ["TONAL_SPOT"] = Variant.TonalSpot,
        ["VIBRANT"] = Variant.Vibrant,
        ["EXPRESSIVE"] = Variant.Expressive,
        ["FIDELITY"] = Variant.Fidelity,
        ["CONTENT"] = Variant.Content,
        ["RAINBOW"] = Variant.Rainbow,
        ["FRUIT_SALAD"] = Variant.FruitSalad,
        ["CMF"] = Variant.Cmf,
    };

    [TestMethod]
    public void TheRolesAreUpstreamsInUpstreamsOrder()
    {
        var roles = Fixture.Load("schemes").GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToList();

        CollectionAssert.AreEqual(roles, RadiantDynamicColors.AllColors.Select(c => c.Name).ToList());
    }

    [TestMethod]
    public void EveryRoleOfEverySchemeMatchesUpstream()
    {
        var fixture = Fixture.Load("schemes");
        var roles = RadiantDynamicColors.AllColors;
        var schemes = 0;
        var colors = 0;
        var mismatches = new List<string>();

        foreach (var row in fixture.GetProperty("schemes").EnumerateArray())
        {
            var label = Describe(row);
            var throws = row.TryGetProperty("throws", out var t) && t.GetBoolean();
            DynamicScheme scheme;
            try
            {
                scheme = Create(row);
            }
            catch (ArgumentException) when (throws)
            {
                continue;
            }
            Assert.IsFalse(throws, $"{label}: upstream refuses this scheme, the port built one");
            schemes++;

            Assert.AreEqual(ParseSpec(row.GetProperty("resolvedSpec").GetString()!), scheme.SpecVersion, $"{label}: resolved spec");
            var values = row.GetProperty("values");
            for (var i = 0; i < roles.Count; i++)
            {
                colors++;
                var expected = Fixture.Argb(values[i]);
                var actual = scheme.GetArgb(roles[i]);
                if (actual != expected)
                {
                    mismatches.Add($"{label} {roles[i].Name}: expected {Fixture.Hex(expected)}, got {Fixture.Hex(actual)}");
                }
            }
        }

        Assert.AreEqual(4000, schemes, "every scheme upstream built should be compared");
        Assert.AreEqual(0, mismatches.Count,
            $"{mismatches.Count} of {colors} colors differ across {schemes} schemes; first:\n{string.Join("\n", mismatches.Take(15))}");
    }

    private static DynamicScheme Create(JsonElement row)
    {
        var sources = row.GetProperty("source").EnumerateArray().Select(s => Hct.FromInt(Fixture.Argb(s))).ToList();
        var variant = s_variants[row.GetProperty("variant").GetString()!];
        var isDark = row.GetProperty("isDark").GetBoolean();
        var contrast = row.GetProperty("contrastLevel").GetDouble();
        var spec = ParseSpec(row.GetProperty("specVersion").GetString()!);
        var platform = row.GetProperty("platform").GetString() == "watch" ? Platform.Watch : Platform.Phone;

        return variant switch
        {
            Variant.Monochrome => new SchemeMonochrome(sources, isDark, contrast, spec, platform),
            Variant.Neutral => new SchemeNeutral(sources, isDark, contrast, spec, platform),
            Variant.TonalSpot => new SchemeTonalSpot(sources, isDark, contrast, spec, platform),
            Variant.Vibrant => new SchemeVibrant(sources, isDark, contrast, spec, platform),
            Variant.Expressive => new SchemeExpressive(sources, isDark, contrast, spec, platform),
            Variant.Fidelity => new SchemeFidelity(sources, isDark, contrast, spec, platform),
            Variant.Content => new SchemeContent(sources, isDark, contrast, spec, platform),
            Variant.Rainbow => new SchemeRainbow(sources, isDark, contrast, spec, platform),
            Variant.FruitSalad => new SchemeFruitSalad(sources, isDark, contrast, spec, platform),
            Variant.Cmf => new SchemeCmf(sources, isDark, contrast, spec, platform),
            _ => throw new ArgumentOutOfRangeException(nameof(row)),
        };
    }

    private static SpecVersion ParseSpec(string spec) => spec switch
    {
        "2021" => SpecVersion.Spec2021,
        "2025" => SpecVersion.Spec2025,
        "2026" => SpecVersion.Spec2026,
        _ => throw new ArgumentOutOfRangeException(nameof(spec), spec, null),
    };

    private static string Describe(JsonElement row) =>
        $"[{string.Join(",", row.GetProperty("source").EnumerateArray().Select(s => Fixture.Hex(Fixture.Argb(s))))}] " +
        $"{row.GetProperty("variant").GetString()} {(row.GetProperty("isDark").GetBoolean() ? "dark" : "light")} " +
        $"c{row.GetProperty("contrastLevel").GetDouble()} {row.GetProperty("specVersion").GetString()} {row.GetProperty("platform").GetString()}";
}
