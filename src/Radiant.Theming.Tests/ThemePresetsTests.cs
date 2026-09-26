using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.ColorSystem;
using Radiant.Graphics2D;
using Radiant.Text;

namespace Radiant.Theming.Tests;

[TestClass]
public class ThemePresetsTests
{
    private static double Tone(Color color) => Hct.FromInt((int)color.ToArgb()).Tone;

    private static double ContrastOf(Color a, Color b) => Contrast.RatioOfTones(Tone(a), Tone(b));

    private static Theme Dark(Theme theme) => theme with { Colors = theme.Colors with { IsDark = true } };

    [TestMethod]
    public void EveryPresetHasADistinctName()
    {
        var names = ThemePresets.All.Select(t => t.Name).ToArray();

        CollectionAssert.AllItemsAreNotNull(names);
        CollectionAssert.AllItemsAreUnique(names);
        Assert.AreSame(ThemePresets.Linen, ThemePresets.Find("linen"));
        Assert.IsNull(ThemePresets.Find("nothing"));
    }

    [TestMethod]
    public void TheTonalPresetIsTheDefaultTheme()
    {
        var standard = new Theme();

        Assert.AreEqual(standard.Colors, ThemePresets.Tonal.Colors);
        Assert.AreEqual(standard.Shape, ThemePresets.Tonal.Shape);
        Assert.AreEqual(standard.Components, ThemePresets.Tonal.Components);
        Assert.AreEqual(standard.Density, ThemePresets.Tonal.Density);
        Assert.AreEqual(standard.Motion, ThemePresets.Tonal.Motion);
    }

    [TestMethod]
    public void EveryPresetsFamiliesAreReadableLightAndDark()
    {
        foreach (var preset in ThemePresets.All)
        {
            foreach (var theme in new[] { ResolvedTheme.Resolve(preset), ResolvedTheme.Resolve(Dark(preset)) })
            {
                foreach (var name in Enum.GetValues<SurfaceName>())
                {
                    foreach (var container in new[] { false, true })
                    {
                        var ratio = ContrastOf(theme.Get(name, false, container), theme.Get(name, true, container));
                        Assert.IsTrue(ratio >= 4.5, $"{preset.Name} {name} container={container} dark={theme.Theme.Colors.IsDark}: {ratio:0.0}");
                    }
                }
            }
        }
    }

    [TestMethod]
    public void HandPickedColoursAreUsedForTheirAppearance()
    {
        var light = ResolvedTheme.Resolve(ThemePresets.Quartz);
        var dark = ResolvedTheme.Resolve(Dark(ThemePresets.Quartz));
        var roles = ThemePresets.Quartz.Colors;

        Assert.AreEqual(roles.Light!.Primary.Color, light.Get(SurfaceName.Primary));
        Assert.AreEqual(roles.Light.Info.OnContainer, light.Get(SurfaceName.Info, on: true, container: true));
        Assert.AreEqual(roles.Light.OnSurfaceVariant, light.Get(SurfaceName.SurfaceContainerHigh, on: true, container: true));
        Assert.AreEqual(roles.Light.Background, light.Background);
        Assert.AreEqual(roles.Dark!.Surface, dark.Get(SurfaceName.Surface));
        Assert.AreEqual(roles.Dark.Outline, dark.Outline);
    }

    [TestMethod]
    public void FixedFamiliesDefaultToTheirFamilysContainer()
    {
        var theme = ResolvedTheme.Resolve(ThemePresets.Linen);
        var primary = ThemePresets.Linen.Colors.Light!.Primary;

        Assert.AreEqual(primary.Container, theme.Get(SurfaceName.PrimaryFixed));
        Assert.AreEqual(primary.OnContainer, theme.Get(SurfaceName.PrimaryFixed, on: true));
    }

    [TestMethod]
    public void RaisedContrastStrengthensQuietTextAndBordersInAFixedPalette()
    {
        foreach (var preset in new[] { ThemePresets.Quartz, Dark(ThemePresets.Linen) })
        {
            var standard = ResolvedTheme.Resolve(preset);
            var high = ResolvedTheme.Resolve(preset with { Colors = preset.Colors with { ContrastLevel = 1 } });
            var surface = standard.Get(SurfaceName.Surface);

            Assert.IsTrue(ContrastOf(high.Get(SurfaceName.SurfaceVariant, on: true), surface) > ContrastOf(standard.Get(SurfaceName.SurfaceVariant, on: true), surface), preset.Name);
            Assert.IsTrue(ContrastOf(high.Outline, surface) > ContrastOf(standard.Outline, surface), preset.Name);
            Assert.AreEqual(standard.Get(SurfaceName.Primary), high.Get(SurfaceName.Primary), "accents stay as picked");
        }
    }

    [TestMethod]
    public void AFixedPaletteCanTakeItsAccentFromTheSeed()
    {
        var seed = Color.FromArgb(0xFF1B7F3A);
        var quartz = ThemePresets.Quartz with { Colors = ThemePresets.Quartz.Colors with { Seed = seed, AccentFromSeed = true } };
        var scheme = new DynamicScheme(Hct.FromInt(unchecked((int)seed.ToArgb())), Variant.TonalSpot, false, 0, ColorSystem.Platform.Phone, SpecVersion.Spec2021);

        var theme = ResolvedTheme.Resolve(quartz);

        Assert.AreEqual((uint)scheme.GetArgb(RadiantDynamicColors.Primary()), theme.Get(SurfaceName.Primary).ToArgb());
        Assert.AreEqual(ThemePresets.Quartz.Colors.Light!.Surface, theme.Get(SurfaceName.Surface), "the neutrals stay");
        Assert.AreEqual(ThemePresets.Quartz.Colors.Light.Error.Color, theme.Get(SurfaceName.Error), "and the status colours");
    }

    [TestMethod]
    public void SeededAccentsAreReadableInEveryPresetLightAndDark()
    {
        var random = new Random(7);
        foreach (var preset in new[] { ThemePresets.Quartz, ThemePresets.Linen })
        {
            for (var i = 0; i < 12; i++)
            {
                var seed = Color.FromArgb(Hct.From(random.NextDouble() * 360, 30 + random.NextDouble() * 60, 50).ToInt());
                foreach (var dark in new[] { false, true })
                {
                    var theme = ResolvedTheme.Resolve(preset with { Colors = preset.Colors with { Seed = seed, AccentFromSeed = true, IsDark = dark } });
                    foreach (var container in new[] { false, true })
                    {
                        var ratio = ContrastOf(theme.Get(SurfaceName.Primary, false, container), theme.Get(SurfaceName.Primary, true, container));
                        Assert.IsTrue(ratio >= 4.5, $"{preset.Name} {seed} dark={dark} container={container}: {ratio:0.0}");
                    }
                }
            }
        }
    }

    [TestMethod]
    public void WithStyleKeepsTakingTheAccentFromTheSeed()
    {
        var mine = ThemePresets.Quartz with { Colors = ThemePresets.Quartz.Colors with { AccentFromSeed = true } };

        Assert.IsTrue(mine.WithStyle(ThemePresets.Linen).Colors.AccentFromSeed);
    }

    [TestMethod]
    public void RestylingKeepsTheColoursAndTakesTheRest()
    {
        var quartz = ResolvedTheme.Resolve(ThemePresets.Quartz);

        var restyled = quartz.Restyled(ThemePresets.Tonal with { Density = -2 });

        Assert.AreEqual(quartz.Get(SurfaceName.Primary), restyled.Get(SurfaceName.Primary));
        Assert.AreEqual(quartz.Background, restyled.Background);
        Assert.AreEqual(TabsLook.Underline, restyled.Theme.Components.Navigation.Tabs);
        Assert.AreEqual(-2, restyled.Theme.Density);
        Assert.AreSame(ThemePresets.Quartz.Colors, restyled.Theme.Colors);
    }

    [TestMethod]
    public void HandPickedColoursIgnoreTheSeed()
    {
        var quartz = ThemePresets.Quartz;
        var reseeded = quartz with { Colors = quartz.Colors with { Seed = Color.FromArgb(0xFF00FF00) } };

        Assert.AreEqual(ResolvedTheme.Resolve(quartz).Get(SurfaceName.Primary), ResolvedTheme.Resolve(reseeded).Get(SurfaceName.Primary));
    }

    [TestMethod]
    public void WithStyleKeepsTheUsersSettings()
    {
        var mine = new Theme
        {
            Colors = new ThemeColors { IsDark = true, ContrastLevel = 0.5, Seed = Color.FromArgb(0xFF336699) },
            Motion = new MotionScheme { Reduced = true },
        };

        var styled = mine.WithStyle(ThemePresets.Linen);

        Assert.AreEqual("Linen", styled.Name);
        Assert.IsTrue(styled.Colors.IsDark);
        Assert.AreEqual(0.5, styled.Colors.ContrastLevel);
        Assert.AreEqual(mine.Colors.Seed, styled.Colors.Seed);
        Assert.IsTrue(styled.Motion.Reduced);
        Assert.AreSame(ThemePresets.Linen.Colors.Dark, styled.Colors.Dark);
        Assert.AreEqual(ThemePresets.Linen.Shape, styled.Shape);
        Assert.AreEqual(ThemePresets.Linen.Components, styled.Components);
    }

    [TestMethod]
    public void ControlsArePillsByDefaultAndGentlyRoundedInOtherPresets()
    {
        Assert.AreEqual(ShapeScale.FullRadius, ResolvedTheme.Resolve(ThemePresets.Tonal).Radius(CornerShapeRole.Control));
        Assert.AreEqual(6f, ResolvedTheme.Resolve(ThemePresets.Quartz).Radius(CornerShapeRole.Control));
        Assert.AreEqual(8f, ResolvedTheme.Resolve(ThemePresets.Linen).Radius(CornerShapeRole.Control));
        Assert.AreEqual(0f, new ShapeScale().Scaled(0).Radius(CornerShapeRole.Control));
    }

    [TestMethod]
    public void AControlsCornersMoveSteadilyFromAPill()
    {
        var from = ResolvedTheme.Resolve(ThemePresets.Tonal);
        var to = ResolvedTheme.Resolve(ThemePresets.Quartz);

        var half = ResolvedTheme.Lerp(from, to, 0.5f).Radius(CornerShapeRole.Control);
        var most = ResolvedTheme.Lerp(from, to, 0.9f).Radius(CornerShapeRole.Control);

        Assert.IsTrue(half is > 6f and < 64f, $"{half}");
        Assert.IsTrue(most < half, $"{most} after {half}");
    }

    [TestMethod]
    public void ThemesCrossFadeBetweenPresets()
    {
        var from = ResolvedTheme.Resolve(ThemePresets.Quartz);
        var to = ResolvedTheme.Resolve(ThemePresets.Linen);

        var middle = ResolvedTheme.Lerp(from, to, 0.5f);

        Assert.AreNotEqual(from.Background, middle.Background);
        Assert.AreNotEqual(to.Background, middle.Background);
        Assert.AreEqual("Linen", middle.Theme.Name);
    }

    [TestMethod]
    public void TheDefaultShadowsAreAnAmbientThenAKeyShadow()
    {
        var shadows = new ElevationScale().Shadows(ElevationLevel.Level2, new System.Numerics.Vector4(0, 0, 0, 1));

        Assert.AreEqual(2, shadows.Count);
        Assert.AreEqual((2f, 6f, 2f, 0.15f), (shadows[0].Offset.Y, shadows[0].Blur, shadows[0].Spread, shadows[0].Color.W));
        Assert.AreEqual((1f, 2f, 0f, 0.3f), (shadows[1].Offset.Y, shadows[1].Blur, shadows[1].Spread, shadows[1].Color.W));
    }

    [TestMethod]
    public void PresetsCanCastAnyNumberOfShadowsWithNegativeSpread()
    {
        var shadows = ThemePresets.Quartz.Elevation.Shadows(ElevationLevel.Level4, new System.Numerics.Vector4(0, 0, 0, 1));

        Assert.AreEqual(1, shadows.Count);
        Assert.AreEqual(-12f, shadows[0].Spread);
        Assert.AreEqual(0, ThemePresets.Quartz.Elevation.Shadows(ElevationLevel.Level0, default).Count);
    }

    [TestMethod]
    public void TheDefaultComponentStylesAreTonal()
    {
        Assert.AreEqual(ComponentStyles.Tonal, new Theme().Components);
        Assert.AreEqual(TabsLook.Underline, ComponentStyles.Tonal.Navigation.Tabs);
        Assert.AreEqual(FieldLook.FloatingLabel, ComponentStyles.Tonal.Field.Look);
        Assert.AreEqual(DisabledLook.Recolor, ComponentStyles.Tonal.Interaction.Disabled);
    }

    [TestMethod]
    public void TheOtherPresetsBuildHairlineComponents()
    {
        foreach (var preset in new[] { ThemePresets.Quartz, ThemePresets.Linen })
        {
            var components = preset.Components;
            Assert.AreEqual(TabsLook.Segmented, components.Navigation.Tabs, preset.Name);
            Assert.AreEqual(FieldLook.LabelAbove, components.Field.Look, preset.Name);
            Assert.AreEqual(SwitchLook.Compact, components.Selection.Switch, preset.Name);
            Assert.AreEqual(DisabledLook.Fade, components.Interaction.Disabled, preset.Name);
            Assert.IsFalse(components.Selection.Halo, preset.Name);
            Assert.AreEqual(IconSet.Outline, components.Icons.Set, preset.Name);
            Assert.IsNull(components.Button.Text.Content, $"{preset.Name}: text buttons are neutral");
        }
        Assert.AreEqual(SurfaceName.Info, ThemePresets.Linen.Components.Interaction.FocusRingColor);
    }

    [TestMethod]
    public void AShadeLayerDarkensEvenAFilledControlOnALightTheme()
    {
        var theme = ResolvedTheme.Resolve(ThemePresets.Linen);
        var filled = SurfaceState.Default.With(new SurfaceChange { Surface = SurfaceName.Primary });

        var hovered = theme.StateLayerColor(filled, 0.1f);

        Assert.IsTrue(Tone(hovered) < Tone(theme.Get(SurfaceName.Primary)), "the shade darkens the accent");
        var content = ResolvedTheme.Resolve(ThemePresets.Quartz).StateLayerColor(filled, 0.1f);
        Assert.IsTrue(Tone(content) > Tone(ThemePresets.Quartz.Colors.Light!.Primary.Color), "content layers lighten a filled control");
    }

    [TestMethod]
    public void AShadeLayerLightensOnADarkTheme()
    {
        var theme = ResolvedTheme.Resolve(Dark(ThemePresets.Linen));

        Assert.IsTrue(Tone(theme.StateLayerColor(SurfaceState.Default, 0.1f)) > Tone(theme.Get(SurfaceName.Surface)));
    }

    [TestMethod]
    public void OnlyHairlineButtonsShrinkWhenPressed()
    {
        Assert.AreEqual(1f, ComponentStyles.Tonal.Interaction.PressScale);
        Assert.IsTrue(ComponentStyles.Hairline.Interaction.PressScale is > 0.9f and < 1f);
    }

    [TestMethod]
    public void OverlinesAreSmallAndSpacedOut()
    {
        var overline = new Theme().Typography[TextType.Overline];

        Assert.IsTrue(overline.Size <= 12f);
        Assert.IsTrue(overline.Tracking > 0.05f);
    }

    [TestMethod]
    public void TheWarmPresetKeepsTheSerifForDisplayText()
    {
        Assert.AreEqual(FontLibrary.SourceSerif, ThemePresets.Linen.Typography[TextType.DisplaySmall].FontFamily);
        Assert.AreEqual(FontLibrary.Inter, ThemePresets.Linen.Typography[TextType.HeadlineLarge].FontFamily);
        Assert.AreEqual(FontLibrary.Inter, ThemePresets.Linen.Typography[TextType.BodyMedium].FontFamily);
        Assert.AreEqual(FontLibrary.JetBrainsMono, ThemePresets.Linen.Typography[TextType.Code].FontFamily);
    }
}
