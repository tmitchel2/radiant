using Radiant.Theming;
using Radiant.UI.Driver;

namespace Radiant.UI.Automation.Tests.Gallery;

/// <summary>The studio page: a whole app built from stock components, in each theme.</summary>
[TestClass]
public sealed class StudioTests : GalleryTest
{
    [TestMethod]
    [DataRow("Tonal")]
    [DataRow("Linen")]
    public async Task SendIsDisabledUntilThereIsAPrompt(string preset)
    {
        Themes.Set(Themes.Theme.WithStyle(ThemePresets.Find(preset)!));
        await GoToAsync("Studio");
        await Driver.StudioPage().Prompt().Send().Expect().ToBeDisabledAsync();

        await Driver.StudioPage().Suggestion().WithLabel("A retro rocket").TapAsync();

        await Driver.StudioPage().Prompt().Field().Input().Expect().ToHaveValueAsync("A retro rocket");
        await Driver.StudioPage().Prompt().Send().Expect().ToBeEnabledAsync();
    }

    [TestMethod]
    public async Task CommandEnterSendsThePromptAndClearsIt()
    {
        await GoToAsync("Studio");
        var input = Driver.StudioPage().Prompt().Field().Input();
        await input.TapAsync();
        await Driver.TypeAsync("A windmill");

        await Driver.KeyAsync("Cmd+Enter");

        await input.Expect().ToHaveValueAsync("");
        await Driver.StudioPage().Prompt().Send().Expect().ToBeDisabledAsync();
    }

    [TestMethod]
    public async Task EnterAloneStartsANewLine()
    {
        await GoToAsync("Studio");
        var input = Driver.StudioPage().Prompt().Field().Input();
        await input.TapAsync();
        await Driver.TypeAsync("A windmill");

        await Driver.KeyAsync("Enter");

        await input.Expect().ToHaveValueAsync("A windmill\n");
    }

    [TestMethod]
    public async Task SpinTogglesOff()
    {
        await GoToAsync("Studio");
        await Driver.StudioPage().Spin().Expect().ToBeCheckedAsync();

        await Driver.StudioPage().Spin().TapAsync();

        await Driver.StudioPage().Spin().Expect().ToBeUncheckedAsync();
    }

    [TestMethod]
    [DataRow("Tonal")]
    [DataRow("Quartz")]
    public async Task TheViewTabsChooseAViewWhateverTheirLook(string preset)
    {
        Themes.Set(Themes.Theme.WithStyle(ThemePresets.Find(preset)!));
        await GoToAsync("Studio");

        await Driver.Role.Tab("Parts").TapAsync();

        await Driver.Role.Tab("Parts").Expect().ToBeSelectedAsync();
        await Driver.Role.Tab("Model").Expect().ToBeUnselectedAsync();
    }

    [TestMethod]
    public async Task ThePlayButtonPauses()
    {
        await GoToAsync("Studio");

        await Driver.StudioPage().PlayPause().WithLabel("Pause").TapAsync();

        await Driver.StudioPage().PlayPause().WithLabel("Play").Expect().ToExistAsync();
    }
}
