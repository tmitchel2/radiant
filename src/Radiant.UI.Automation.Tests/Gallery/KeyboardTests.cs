using Radiant.UI.Driver;

namespace Radiant.UI.Automation.Tests.Gallery;

/// <summary>The gallery by keyboard alone: focus goes where a person expects, and keys act.</summary>
[TestClass]
public sealed class KeyboardTests : GalleryTest
{
    [TestMethod]
    public async Task TheSignInFormIsFilledInByKeyboard()
    {
        await GoToAsync("Sign in");
        var form = Driver.SignInForm();
        await form.Email().FocusAsync();

        await Driver.TypeAsync("ada@example.com");
        await Driver.KeyAsync("Tab");
        await form.Password().Expect().ToBeFocusedAsync();
        await Driver.TypeAsync("secret");
        await Driver.KeyAsync("Tab");
        await form.Password().TrailingButton().Expect().ToBeFocusedAsync();
        await Driver.KeyAsync("Tab");
        await form.RememberMe().Expect().ToBeFocusedAsync();
        await Driver.KeyAsync("Space");
        await form.RememberMe().Expect().ToBeUncheckedAsync();
        await Driver.KeyAsync("Tab");
        await form.ForgotPassword().Expect().ToBeFocusedAsync();
        await Driver.KeyAsync("Tab");
        await form.SignIn().Expect().ToBeFocusedAsync();

        await form.Email().Expect().ToHaveValueAsync("ada@example.com");
        await form.Password().Input().Expect().ToHaveValueAsync("••••••");
    }

    [TestMethod]
    public async Task ShiftTabGoesBack()
    {
        await GoToAsync("Sign in");
        await Driver.SignInForm().Password().FocusAsync();

        await Driver.KeyAsync("Shift+Tab");

        await Driver.SignInForm().Email().Expect().ToBeFocusedAsync();
    }

    [TestMethod]
    public async Task TheSidebarIsWorkedByArrows()
    {
        await Destination("Components").FocusAsync();

        await Driver.KeyAsync("Down");
        await Driver.KeyAsync("Enter");

        await Destination("Dashboard").Expect().ToBeSelectedAsync();
    }

    [TestMethod]
    public async Task ButtonsPressWithSpaceAndEnter()
    {
        await Driver.VerticalSlice().Filled().FocusAsync();

        await Driver.KeyAsync("Space");
        await Driver.KeyAsync("Enter");

        await Text("Filled pressed 2 times").Expect().ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task TheWizardIsWorkedByKeyboard()
    {
        await GoToAsync("New project");
        var template = Driver.NewProjectPage().Template();
        await template.Option().WithLabel("Empty app").FocusAsync();

        await Driver.KeyAsync("Down");

        await template.Option().WithLabel("Sidebar app").Expect().ToBeCheckedAsync();
        await template.Option().WithLabel("Sidebar app").Expect().ToBeFocusedAsync();
        await Driver.KeyAsync("Up");
        await Driver.KeyAsync("Up");
        await template.Option().WithLabel("Document editor").Expect().ToBeCheckedAsync();
    }

    [TestMethod]
    public async Task TabGoesPastARadioGroupNotThroughIt()
    {
        await GoToAsync("New project");
        await Driver.NewProjectPage().Template().Option().WithLabel("Empty app").FocusAsync();

        await Driver.KeyAsync("Tab");

        await Driver.NewProjectPage().Template().Option().Focused().Expect().ToBeGoneAsync();
    }
}
