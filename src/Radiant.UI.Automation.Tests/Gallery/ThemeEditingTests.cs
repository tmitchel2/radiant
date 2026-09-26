using Radiant.ColorSystem;
using Radiant.Gallery.ThemeLab;
using Radiant.Text;
using Radiant.Theming;

namespace Radiant.UI.Automation.Tests.Gallery;

/// <summary>What counts as an edited theme, and type edits worked out from the preset's scale.</summary>
[TestClass]
public sealed class ThemeEditingTests
{
    [TestMethod]
    [DataRow("Tonal")]
    [DataRow("Quartz")]
    [DataRow("Linen")]
    public void APresetIsNotModifiedWhateverTheUsersSettings(string name)
    {
        var preset = ThemePresets.Find(name)!;
        var theme = new Theme().WithStyle(preset) with
        {
            Colors = preset.Colors with { IsDark = true, ContrastLevel = 0.5, Seed = Graphics2D.Color.FromArgb(0xFF336699) },
            Motion = preset.Motion with { Reduced = true },
        };

        Assert.IsFalse(ThemeEditing.IsModified(theme));
    }

    [TestMethod]
    public void ChangingTheLookIsAModification()
    {
        var theme = ThemePresets.Tonal with { Colors = ThemePresets.Tonal.Colors with { Variant = Variant.Vibrant } };

        Assert.IsTrue(ThemeEditing.IsModified(theme));
        Assert.AreEqual(Variant.TonalSpot, ThemeEditing.Reset(theme).Colors.Variant);
    }

    [TestMethod]
    public void ATypeEditMovesEachRolesWeightAndPuttingItBackGivesThePresetsScale()
    {
        var preset = ThemePresets.Quartz;

        var edited = TypographyEditing.WithWeight(TypographyEditing.WithScale(preset, 1.25f), TypeGroup.Headline, preset.Typography[TextType.HeadlineSmall].Weight + 100);

        Assert.AreEqual(1.25f, TypographyEditing.Scale(edited));
        Assert.AreEqual(preset.Typography[TextType.HeadlineLarge].Weight + 100, edited.Typography[TextType.HeadlineLarge].Weight);
        Assert.AreEqual(preset.Typography[TextType.BodyMedium].Size * 1.25f, edited.Typography[TextType.BodyMedium].Size);
        Assert.IsTrue(ThemeEditing.IsModified(edited));

        var back = TypographyEditing.WithWeight(TypographyEditing.WithScale(edited, 1f), TypeGroup.Headline, preset.Typography[TextType.HeadlineSmall].Weight);

        Assert.AreSame(preset.Typography, back.Typography);
        Assert.IsFalse(ThemeEditing.IsModified(back));
    }

    [TestMethod]
    public void EnumNamesReadAsWords()
    {
        Assert.AreEqual("Surface container high", ThemeFields.Words(nameof(SurfaceName.SurfaceContainerHigh)));
        Assert.AreEqual("Level 3", ThemeFields.Words(nameof(ElevationLevel.Level3)));
        Assert.AreEqual("Spec 2021", ThemeFields.Words(nameof(SpecVersion.Spec2021)));
    }
}
