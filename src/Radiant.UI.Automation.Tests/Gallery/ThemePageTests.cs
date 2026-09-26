using Radiant.Components;
using Radiant.Gallery;
using Radiant.Gallery.ThemeLab;
using Radiant.Text;
using Radiant.Theming;
using Radiant.UI.Driver;

namespace Radiant.UI.Automation.Tests.Gallery;

/// <summary>The theme page: choosing a preset, editing the theme live, and putting it back.</summary>
[TestClass]
public sealed class ThemePageTests : GalleryTest
{
    [TestMethod]
    public async Task ThePaletteButtonOpensTheThemePage()
    {
        await Driver.ThemePicker().Button().TapAsync();

        await Destination("Theme").Expect().ToBeSelectedAsync();
        await Title("Theme").Expect().ToExistAsync();
        await Driver.ThemeEditor().Preset().Expect().ToBeVisibleAsync();
    }

    [TestMethod]
    [DataRow("Quartz")]
    [DataRow("Linen")]
    public async Task ChoosingAPresetKeepsDarkMode(string preset)
    {
        await Driver.KeyAsync("Cmd+Shift+D");
        await OpenAsync();

        await Text(preset).Within(Driver.ThemeEditor().Preset()).TapAsync();
        await Driver.WaitForIdleAsync();

        Assert.AreEqual(preset, Themes.Theme.Name);
        Assert.IsTrue(Themes.Theme.Colors.IsDark, "dark mode is kept");
        Assert.AreSame(ThemePresets.Find(preset)!.Colors.Dark, Themes.Theme.Colors.Dark);
    }

    [TestMethod]
    public async Task TheDarkSwitchSwapsLightAndDark()
    {
        await OpenAsync();

        await Driver.ThemeEditor().Dark().TapAsync();

        await Driver.ThemeEditor().Dark().Expect().ToBeCheckedAsync();
        Assert.IsTrue(Themes.Theme.Colors.IsDark);
    }

    [TestMethod]
    public async Task TheSeedTakesAHexColour()
    {
        await OpenAsync();

        await SetColourAsync("Seed", "Scheme", "#336699");

        Assert.AreEqual(0x336699L, (long)Themes.Theme.Colors.Seed.ToArgb() & 0xFFFFFF);
    }

    [TestMethod]
    public async Task EditingAPaletteColourChangesOnlyThePaletteShowing()
    {
        Themes.Set(Themes.Theme.WithStyle(ThemePresets.Quartz));
        await OpenAsync();

        await SetColourAsync("Colour", "Primary", "#008080");

        Assert.AreEqual(0x008080L, (long)Themes.Theme.Colors.Light!.Primary.Color.ToArgb() & 0xFFFFFF);
        Assert.AreSame(ThemePresets.Quartz.Colors.Dark, Themes.Theme.Colors.Dark, "the dark palette is untouched");
    }

    [TestMethod]
    public async Task ARadiusSliderMovesByKey()
    {
        await OpenAsync("Shape");

        await Driver.ThemeEditor().Number().Within(Row("Medium", "Radii")).FocusAsync();
        await Driver.KeyAsync("Right");
        await Driver.WaitForIdleAsync();

        Assert.AreEqual(13f, Themes.Theme.Shape.Medium);
    }

    [TestMethod]
    public async Task ControlsCanBeSquared()
    {
        await OpenAsync("Shape");

        await TapAsync(Driver.ThemeEditor().Toggle().Within(Row("Pill controls", "Controls")));
        await Driver.WaitForIdleAsync();

        Assert.AreEqual(8f, Themes.Theme.Shape.Control);
        await Driver.ThemeEditor().Number().Within(Row("Control radius", "Controls")).Expect().ToExistAsync();
    }

    [TestMethod]
    public async Task TheBodyFontCanBeChangedAlone()
    {
        await OpenAsync("Type");

        await ChooseAsync(Row("Font", "Body"), FontLibrary.SourceSerif);

        Assert.AreEqual(FontLibrary.SourceSerif, Themes.Theme.Typography[TextType.BodyMedium].FontFamily);
        Assert.AreEqual(FontLibrary.SourceSerif, Themes.Theme.Typography[TextType.BodySmall].FontFamily);
        Assert.AreEqual(FontLibrary.Inter, Themes.Theme.Typography[TextType.DisplaySmall].FontFamily);
    }

    [TestMethod]
    public async Task AComponentStyleCanBeChanged()
    {
        await OpenAsync("Styles");
        await Driver.ThemeEditor().Family().Button().TapAsync();
        await Driver.Role.MenuItem("Fields").TapAsync();

        await ChooseAsync(Row("Look", "Fields"), "Label above");

        Assert.AreEqual(FieldLook.LabelAbove, Themes.Theme.Components.Field.Look);
    }

    [TestMethod]
    public async Task AVariantsLookCanBeChanged()
    {
        await OpenAsync("Styles");
        await TapAsync(Driver.Role.Button("Filled look"));

        await ChooseAsync(Row("Surface", "Filled look"), "Tertiary");

        Assert.AreEqual(SurfaceName.Tertiary, Themes.Theme.Components.Button.Filled.Surface);
    }

    [TestMethod]
    public async Task EditsShowAsModifiedAndResetPutsThePresetBack()
    {
        Themes.Set(Themes.Theme.WithStyle(ThemePresets.Quartz));
        await OpenAsync("Shape");
        await Text("Modified").Expect().ToBeGoneAsync();

        await Driver.ThemeEditor().Number().Within(Row("Medium", "Radii")).FocusAsync();
        await Driver.KeyAsync("Right");
        await Text("Modified").Expect().ToBeVisibleAsync();
        await Driver.ThemeEditor().Reset().TapAsync();
        await Driver.WaitForIdleAsync();

        Assert.AreEqual(Themes.Theme.WithStyle(ThemePresets.Quartz), Themes.Theme);
        await Text("Modified").Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task EditsLastWhenLeavingThePage()
    {
        await OpenAsync("Shape");
        await TapAsync(Driver.ThemeEditor().Toggle().Within(Row("Pill controls", "Controls")));

        await GoToAsync("Dashboard");
        await GoToAsync("Theme");

        Assert.AreEqual(8f, Themes.Theme.Shape.Control);
        await Text("Modified").Expect().ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task ChoosingAnotherPresetOffersToUndo()
    {
        await OpenAsync("Shape");
        await TapAsync(Driver.ThemeEditor().Toggle().Within(Row("Pill controls", "Controls")));
        await Driver.WaitForIdleAsync();
        var edited = Themes.Theme;

        await Text("Linen").Within(Driver.ThemeEditor().Preset()).TapAsync();
        await Driver.SnackbarHost().Action().TapAsync();
        await Driver.WaitForIdleAsync();

        Assert.AreEqual(edited, Themes.Theme);
    }

    [TestMethod]
    public async Task TheFilterFindsAStyleProperty()
    {
        await OpenAsync("Styles");

        await Driver.ThemeEditor().Properties().Filter().TypeAsync("height");

        await Row("Height", "Buttons").Expect().ToBeVisibleAsync();
        await Driver.Role.Group("Filled look").Expect().ToBeGoneAsync();
    }

    // Opens the theme page, and one of its tabs.
    private async Task OpenAsync(string? tab = null)
    {
        await GoToAsync("Theme");
        if (tab is not null)
        {
            await Driver.Role.Tab(tab).TapAsync();
            await Driver.Role.Tab(tab).Expect().ToBeSelectedAsync();
        }
    }

    // Scrolls the editor to a control, as someone would, and taps it.
    private static async Task TapAsync(Locator control)
    {
        await SeeAsync(control);
        await control.TapAsync();
    }

    // A property: its row, in its section.
    private Locator Row(string name, string section) => Driver.Role.Group(name).Within(Driver.Role.Group(section));

    private async Task ChooseAsync(Locator row, string option)
    {
        await TapAsync(Driver.ChoiceButton().Within(row).Button());
        await Driver.Role.MenuItem(option).TapAsync();
        await Driver.WaitForIdleAsync();
    }

    private async Task SetColourAsync(string name, string section, string hex)
    {
        await TapAsync(Driver.ColorSwatchField().Within(Row(name, section)).Swatch());
        await Driver.ColorSwatchField().Picker().HexField().TypeAsync(hex, replace: true, submit: true);
        await Driver.WaitForIdleAsync();
    }
}
