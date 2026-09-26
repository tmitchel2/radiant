using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Driver;

namespace Radiant.UI.Automation.Tests.Gallery;

/// <summary>The application templates' pages: dashboard, settings, sign-in, empty state, table, landing page and store.</summary>
[TestClass]
public sealed class ApplicationPagesTests : GalleryTest
{
    [TestMethod]
    public async Task TheDashboardShowsItsFiguresAndActions()
    {
        await GoToAsync("Dashboard");

        await Driver.DashboardPage().Export().Expect().ToBeHittableAsync();
        await Driver.DashboardPage().NewReport().Expect().ToBeHittableAsync();
        await Text("$48,210").Expect().ToBeVisibleAsync();
        await SeeAsync(Text("Recent activity"));
    }

    [TestMethod]
    public async Task TheDarkThemeSwitchChangesTheTheme()
    {
        await GoToAsync("Settings");
        var dark = Driver.SettingsPage().DarkTheme();
        await dark.Expect().ToBeUncheckedAsync();

        await dark.TapAsync();

        await dark.Expect().ToBeCheckedAsync();
        Assert.IsTrue(Themes.Theme.Colors.IsDark);
    }

    [TestMethod]
    public async Task TheDensityIsChosenFromAList()
    {
        await GoToAsync("Settings");

        await Driver.SettingsPage().Density().TapAsync();
        await Driver.Menu().Item().WithLabel("Compact").TapAsync();

        await Driver.SettingsPage().Density().Field().Expect().ToHaveValueAsync("Compact");
    }

    [TestMethod]
    public async Task NotificationSwitchesToggle()
    {
        await GoToAsync("Settings");

        await Driver.SettingsPage().Notifications().TapAsync();
        await Driver.SettingsPage().Digest().TapAsync();

        await Driver.SettingsPage().Notifications().Expect().ToBeUncheckedAsync();
        await Driver.SettingsPage().Digest().Expect().ToBeCheckedAsync();
    }

    [TestMethod]
    public async Task TheAccentTakesAHexColour()
    {
        await GoToAsync("Settings");

        await Driver.SettingsPage().Accent().HexField().TypeAsync("#336699", replace: true, submit: true);

        await Driver.WaitForIdleAsync();
        Assert.AreEqual(0x336699L, (long)Themes.Theme.Colors.Seed.ToArgb() & 0xFFFFFF);
    }

    [TestMethod]
    public async Task SigningInWithNothingSaysWhatsMissing()
    {
        await GoToAsync("Sign in");

        await Driver.SignInForm().SignIn().TapAsync();

        await Text("Enter your email").Expect().ToBeVisibleAsync();
        await Text("Enter your password").Expect().ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task ABadEmailIsCaughtAndAGoodOnePasses()
    {
        await GoToAsync("Sign in");
        var form = Driver.SignInForm();

        await form.Email().TypeAsync("not an email");
        await form.Password().TypeAsync("secret");
        await form.SignIn().TapAsync();
        await Text("Enter an email address, like name@example.com").Expect().ToBeVisibleAsync();

        await form.Email().FillAsync("ada@example.com");
        await form.SignIn().TapAsync();
        await Text("Enter an email address, like name@example.com").Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task ThePasswordCanBeShown()
    {
        await GoToAsync("Sign in");
        var password = Driver.SignInForm().Password();
        await password.TypeAsync("hunter2");
        await password.Input().Expect().ToHaveValueAsync("•••••••");

        await password.TrailingButton().TapAsync();

        await password.Input().Expect().ToHaveValueAsync("hunter2");
        await password.TrailingButton().Expect().ToHaveTextAsync("Hide password");
    }

    [TestMethod]
    public async Task RememberMeToggles()
    {
        await GoToAsync("Sign in");
        await Driver.SignInForm().RememberMe().Expect().ToBeCheckedAsync();

        await Driver.SignInForm().RememberMe().TapAsync();

        await Driver.SignInForm().RememberMe().Expect().ToBeUncheckedAsync();
    }

    [TestMethod]
    public async Task TheEmptyStateOffersToCompose()
    {
        await GoToAsync("Empty state");

        await Text("No messages yet").Expect().ToBeVisibleAsync();
        await Driver.EmptyPage().Compose().Expect().ToBeHittableAsync();
    }

    [TestMethod]
    public async Task TheTableSortsByAColumn()
    {
        await GoToAsync("Table");

        await Driver.Role.ColumnHeader("Age").TapAsync();

        // A row is named by its cells: name, age, city, status.
        var first = await Driver.DataTable().Row().First().TextAsync();
        StringAssert.Contains(first, " 18 ", $"the youngest first after sorting by age: {first}");
        await Driver.Role.ColumnHeader("Age").TapAsync();
        StringAssert.Contains(await Driver.DataTable().Row().First().TextAsync(), " 77 ", "then the oldest");
    }

    [TestMethod]
    public async Task TableRowsAreSelectedByTheirCheckBoxes()
    {
        await GoToAsync("Table");

        await Driver.DataTable().SelectRow().Nth(0).TapAsync();
        await Driver.DataTable().SelectRow().Nth(2).TapAsync();

        await Text("100,000 rows, 2 selected").Expect().ToExistAsync();
        await Driver.DataTable().SelectAll().TapAsync();
        await Text("100,000 rows, 100,000 selected").Expect().ToExistAsync();
    }

    [TestMethod]
    public async Task TheTableScrollsThroughAHundredThousandRows()
    {
        await GoToAsync("Table");

        await Driver.DataTable().Row().First().TapAsync();
        await Driver.KeyAsync("End");

        // Rows are built as they come into view; the hundred-thousandth is there once scrolled to.
        await Driver.DataTable().Row().WithText("100000").Expect().ToBeVisibleAsync();
        await Driver.DataTable().Row().WithText("100000").Expect().ToBeSelectedAsync();
    }

    [TestMethod]
    public async Task TheLandingPageAnswersQuestions()
    {
        await GoToAsync("Landing page");

        await Driver.Role.Button("Is it accessible?").TapAsync();

        await SeeAsync(Text("Components expose roles, names and states, and VoiceOver can read and press them."));
    }

    [TestMethod]
    public async Task TheNewsletterChecksTheAddressThenThanks()
    {
        await GoToAsync("Landing page");
        var newsletter = Driver.Newsletter();

        await newsletter.Subscribe().TapAsync();
        await SeeAsync(Text("Enter your email"));
        await newsletter.Email().TypeAsync("ada@example.com");
        await newsletter.Subscribe().TapAsync();

        await SeeAsync(Text("Thanks! Check your inbox to confirm."));
    }

    [TestMethod]
    public async Task TheLandingPagesActionsCanBeReached()
    {
        await GoToAsync("Landing page");

        await Driver.MarketingPage().GetStarted().Expect().ToBeHittableAsync();
        await Driver.MarketingPage().TalkToSales().TapAsync();
        await Driver.SiteFooter().Link().WithLabel("Privacy").TapAsync();
    }

    [TestMethod]
    public async Task AddingToTheCartUpdatesItAndSaysSo()
    {
        await GoToAsync("Store");
        await Text("Aurora lamp").Expect().ToExistAsync();

        await Driver.ProductCard().Nth(1).AddToCart().TapAsync();

        await Driver.Role.Alert("Added Tide mug to your cart").Expect().ToBeVisibleAsync();
        await Driver.CartSummary().Containing("Tide mug").Expect().ToExistAsync();
    }

    [TestMethod]
    public async Task TheCartsQuantitiesChange()
    {
        await GoToAsync("Store");
        var cart = Driver.CartSummary();

        // A lamp at $89 and two throws at $120: more lamps, then fewer until there are none.
        await cart.Containing("$329.00").Expect().ToExistAsync();
        await cart.More().First().TapAsync();
        await cart.Containing("$418.00").Expect().ToExistAsync();
        await cart.Fewer().First().TapAsync();
        await cart.Fewer().First().TapAsync();

        await cart.Containing("Aurora lamp").Expect().ToBeGoneAsync();
        await cart.Containing("$240.00").Expect().ToExistAsync();
    }

    [TestMethod]
    public async Task OrdersCanBeBoughtAgain()
    {
        await GoToAsync("Store");

        await Driver.OrderHistory().BuyAgain().First().TapAsync();

        await Driver.Role.Alert("Added order WU88191111 to your cart").Expect().ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task ASnackbarGoesAfterItsTimeout()
    {
        await GoToAsync("Store");
        await Driver.Reviews().WriteReview().TapAsync();
        await Driver.Role.Alert("Thanks! Reviews open once your order arrives.").Expect().ToBeVisibleAsync();

        await Driver.StepAsync(TimeSpan.FromSeconds(10));

        await Driver.Role.Alert().Expect().ToBeGoneAsync();
    }
}
