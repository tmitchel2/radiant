using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.ColorSystem;
using Radiant.Graphics2D;

namespace Radiant.Theming.Tests;

[TestClass]
public class ResolvedThemeTests
{
    private static double Tone(Color color) => Hct.FromInt((int)color.ToArgb()).Tone;

    private static double ContrastOf(Color a, Color b) => Contrast.RatioOfTones(Tone(a), Tone(b));

    [TestMethod]
    public void RolesComeFromTheScheme()
    {
        var theme = ResolvedTheme.Resolve(new Theme());
        var scheme = new DynamicScheme(Hct.FromInt(unchecked((int)0xFF6750A4)), Variant.TonalSpot, false, 0, ColorSystem.Platform.Phone, SpecVersion.Spec2021);

        Assert.AreEqual((uint)scheme.GetArgb(RadiantDynamicColors.Primary()), theme.Get(SurfaceName.Primary).ToArgb());
        Assert.AreEqual((uint)scheme.GetArgb(RadiantDynamicColors.OnPrimaryContainer()), theme.Get(SurfaceName.Primary, on: true, container: true).ToArgb());
        Assert.AreEqual((uint)scheme.GetArgb(RadiantDynamicColors.SurfaceContainer()), theme.Get(SurfaceName.Surface, container: true).ToArgb());
        Assert.AreEqual((uint)scheme.GetArgb(RadiantDynamicColors.Outline()), theme.Outline.ToArgb());
    }

    [TestMethod]
    public void EveryFamilysContentIsReadableOnIt()
    {
        foreach (var dark in new[] { false, true })
        {
            var theme = ResolvedTheme.Resolve(new Theme { Colors = new ThemeColors { IsDark = dark } });
            foreach (var name in Enum.GetValues<SurfaceName>())
            {
                foreach (var container in new[] { false, true })
                {
                    var ratio = ContrastOf(theme.Get(name, false, container), theme.Get(name, true, container));
                    Assert.IsTrue(ratio >= 4.5, $"{name} container={container} dark={dark}: {ratio:0.0}");
                }
            }
        }
    }

    [TestMethod]
    public void HighContrastRaisesContrast()
    {
        var standard = ResolvedTheme.Resolve(new Theme());
        var high = ResolvedTheme.Resolve(new Theme { Colors = new ThemeColors { ContrastLevel = 1 } });

        Assert.IsTrue(ContrastOf(high.Get(SurfaceName.Primary, container: true), high.Get(SurfaceName.Primary, true, true))
            > ContrastOf(standard.Get(SurfaceName.Primary, container: true), standard.Get(SurfaceName.Primary, true, true)));
    }

    [TestMethod]
    public void DarkThemesHaveDarkSurfaces()
    {
        var light = ResolvedTheme.Resolve(new Theme());
        var dark = ResolvedTheme.Resolve(new Theme { Colors = new ThemeColors { IsDark = true } });

        Assert.IsTrue(Tone(light.Get(SurfaceName.Surface)) > 90);
        Assert.IsTrue(Tone(dark.Get(SurfaceName.Surface)) < 10);
    }

    [TestMethod]
    public void CustomColoursAreHarmonisedTowardsTheSeed()
    {
        var colors = new ThemeColors { Seed = Color.FromArgb(0xFF0061A4) };
        var harmonised = ResolvedTheme.Resolve(new Theme { Colors = colors });
        var raw = ResolvedTheme.Resolve(new Theme { Colors = colors with { HarmonizeCustomColors = false } });
        var seedHue = Hct.FromInt((int)colors.Seed.ToArgb()).Hue;

        double Distance(Color c)
        {
            var d = Math.Abs(Hct.FromInt((int)c.ToArgb()).Hue - seedHue) % 360;
            return d > 180 ? 360 - d : d;
        }

        Assert.IsTrue(Distance(harmonised.Get(SurfaceName.Success)) < Distance(raw.Get(SurfaceName.Success)));
    }

    [TestMethod]
    public void ARoleStatesOpacityIsApplied()
    {
        var theme = ResolvedTheme.Default;

        var color = theme.Get(new SurfaceRoleState(SurfaceName.Surface, true, false, Legibility.Medium));

        Assert.AreEqual(Legibility.Medium, color.A, 1e-6f);
    }

    [TestMethod]
    public void ShapesAndTypeComeFromTheirScales()
    {
        var theme = ResolvedTheme.Resolve(new Theme { Shape = new ShapeScale().Scaled(0.5f) });

        Assert.AreEqual(6f, theme.Radius(CornerShapeRole.Medium));
        Assert.AreEqual(0f, theme.Radius(CornerShapeRole.None));
        Assert.AreEqual(14f, theme.Text(TextType.BodyMedium).Size);
        Assert.AreEqual(2, theme.Elevation(ElevationLevel.Level1).Count);
        Assert.AreEqual(0, theme.Elevation(ElevationLevel.Level0).Count);
    }

    [TestMethod]
    public void TransitionsMixColoursAndShapesButSnapType()
    {
        var from = ResolvedTheme.Resolve(new Theme());
        var to = ResolvedTheme.Resolve(new Theme
        {
            Colors = new ThemeColors { Seed = Color.FromArgb(0xFFB3261E), IsDark = true },
            Shape = new ShapeScale().Scaled(2f),
            Typography = TypeScale.Default.Scaled(1.25f),
        });

        var half = ResolvedTheme.Lerp(from, to, 0.5f);

        Assert.AreSame(from, ResolvedTheme.Lerp(from, to, 0f));
        Assert.AreSame(to, ResolvedTheme.Lerp(from, to, 1f));
        Assert.AreEqual(18f, half.Radius(CornerShapeRole.Medium), 1e-4f);
        Assert.AreEqual(to.Text(TextType.BodyMedium).Size, half.Text(TextType.BodyMedium).Size);
        var tone = Tone(half.Get(SurfaceName.Surface));
        Assert.IsTrue(tone > Tone(to.Get(SurfaceName.Surface)) && tone < Tone(from.Get(SurfaceName.Surface)), $"{tone}");
    }
}
