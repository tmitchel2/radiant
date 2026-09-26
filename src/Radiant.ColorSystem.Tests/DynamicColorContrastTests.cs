using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.ColorSystem.Tests;

/// <summary>
/// The accessibility guarantee the dynamic color system makes (upstream's "generates colors
/// respecting contrast"): text on its surface reaches a 4.5 contrast ratio at standard contrast or
/// above, and 3.0 at reduced contrast, for every source color, variant, mode and contrast level.
/// </summary>
[TestClass]
public class DynamicColorContrastTests
{
    private static readonly (string Foreground, string Background)[] s_textSurfacePairs =
    [
        ("on_primary", "primary"),
        ("on_primary_container", "primary_container"),
        ("on_secondary", "secondary"),
        ("on_secondary_container", "secondary_container"),
        ("on_tertiary", "tertiary"),
        ("on_tertiary_container", "tertiary_container"),
        ("on_error", "error"),
        ("on_error_container", "error_container"),
        ("on_background", "background"),
        ("on_surface_variant", "surface_bright"),
        ("on_surface_variant", "surface_dim"),
    ];

    [TestMethod]
    public void TextReachesItsContrastRatioOnItsSurfaceInEveryScheme()
    {
        var byName = RadiantDynamicColors.AllColors.ToDictionary(c => c.Name);
        var failures = new List<string>();
        var checkedPairs = 0;

        foreach (var scheme in Schemes())
        {
            var minimum = scheme.ContrastLevel >= 0.0 ? 4.5 : 3.0;
            foreach (var (foreground, background) in s_textSurfacePairs)
            {
                var fgTone = byName[foreground].GetHct(scheme).Tone;
                var bgTone = byName[background].GetHct(scheme).Tone;
                var ratio = Contrast.RatioOfTones(fgTone, bgTone);
                checkedPairs++;
                if (ratio < minimum)
                {
                    failures.Add($"{scheme}: {foreground} on {background} is {ratio:F2}, needed {minimum}");
                }
            }
        }

        Assert.AreEqual(504 * s_textSurfacePairs.Length, checkedPairs);
        Assert.AreEqual(0, failures.Count, string.Join("\n", failures.Take(10)));
    }

    private static IEnumerable<DynamicScheme> Schemes()
    {
        int[] seeds = [unchecked((int)0xFFFF0000), unchecked((int)0xFFFFFF00), unchecked((int)0xFF00FF00), unchecked((int)0xFF0000FF)];
        double[] levels = [-1.0, -0.75, -0.5, -0.25, 0.0, 0.25, 0.5, 0.75, 1.0];
        foreach (var seed in seeds)
        {
            var color = Hct.FromInt(seed);
            foreach (var level in levels)
            {
                foreach (var isDark in new[] { false, true })
                {
                    yield return new SchemeContent(color, isDark, level);
                    yield return new SchemeExpressive(color, isDark, level);
                    yield return new SchemeFidelity(color, isDark, level);
                    yield return new SchemeMonochrome(color, isDark, level);
                    yield return new SchemeNeutral(color, isDark, level);
                    yield return new SchemeTonalSpot(color, isDark, level);
                    yield return new SchemeVibrant(color, isDark, level);
                }
            }
        }
    }
}
