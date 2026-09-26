using Radiant.Gallery;
using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Driver;

namespace Radiant.UI.Automation.Tests.Gallery;

/// <summary>The components page: each control on it does what it says.</summary>
[TestClass]
public sealed class ComponentsPageTests : GalleryTest
{
    [TestMethod]
    public async Task PressingAButtonCountsIt()
    {
        await Driver.VerticalSlice().Filled().TapAsync();
        await Driver.VerticalSlice().Filled().TapAsync();

        await Text("Filled pressed 2 times").Expect().ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task EveryButtonCanBePressed()
    {
        var slice = Driver.VerticalSlice();
        foreach (var button in new[] { slice.Tonal(), slice.Outlined(), slice.TextButton(), slice.Elevated(), slice.Error(), slice.Success(), slice.Add(), slice.Download(), slice.SearchIcon(), slice.Favourite(), slice.Settings(), slice.DeleteIcon() })
        {
            await button.Expect().ToBeEnabledAsync();
            await button.TapAsync();
        }
        await slice.DisabledButton().Expect().ToBeDisabledAsync();
    }

    [TestMethod]
    public async Task ShufflingTheThemeChangesIt()
    {
        var before = Themes.Theme;

        await Driver.VerticalSlice().ShuffleTheme().TapAsync();

        Assert.AreNotEqual(before, Themes.Theme);
    }

    [TestMethod]
    public async Task CheckBoxesToggleButADisabledOneDoesNot()
    {
        var slice = Driver.VerticalSlice();
        await slice.IAgree().Expect().ToBeCheckedAsync();
        await slice.NotifyMe().Expect().ToBeUncheckedAsync();

        await slice.IAgree().TapAsync();
        await slice.NotifyMe().TapAsync();
        await slice.DisabledCheckbox().TapAsync(force: true);

        await slice.IAgree().Expect().ToBeUncheckedAsync();
        await slice.NotifyMe().Expect().ToBeCheckedAsync();
        await slice.DisabledCheckbox().Expect().ToBeCheckedAsync();
        await slice.DisabledCheckbox().Expect().ToBeDisabledAsync();
    }

    [TestMethod]
    public async Task TheSwitchesMirrorEachOther()
    {
        var slice = Driver.VerticalSlice();
        await slice.WiFi().Expect().ToBeCheckedAsync();
        await slice.WiFiOff().Expect().ToBeUncheckedAsync();

        await slice.WiFi().TapAsync();

        await slice.WiFi().Expect().ToBeUncheckedAsync();
        await slice.WiFiOff().Expect().ToBeCheckedAsync();
    }

    [TestMethod]
    public async Task ChoosingARadioUnchoosesTheOther()
    {
        var size = Driver.VerticalSlice().Size();
        await size.Option().WithLabel("Medium").Expect().ToBeCheckedAsync();

        await size.Option().WithLabel("Small").TapAsync();

        await size.Option().WithLabel("Small").Expect().ToBeCheckedAsync();
        await size.Option().WithLabel("Medium").Expect().ToBeUncheckedAsync();
    }

    [TestMethod]
    public async Task TheDialogOpensAndClosesByEachWay()
    {
        var slice = Driver.VerticalSlice();
        await slice.OpenDialog().TapAsync();
        await Driver.Dialog().Panel().Expect().ToBeVisibleAsync();
        await slice.Cancel().TapAsync();
        await Driver.Dialog().Panel().Expect().ToBeGoneAsync();

        await slice.OpenDialog().TapAsync();
        await slice.ConfirmDelete().TapAsync();
        await Driver.Dialog().Panel().Expect().ToBeGoneAsync();

        await slice.OpenDialog().TapAsync();
        await Driver.KeyAsync("Escape");
        await Driver.Dialog().Panel().Expect().ToBeGoneAsync();
        await slice.OpenDialog().Expect().ToBeFocusedAsync();
    }

    [TestMethod]
    public async Task TheDialogKeepsFocusInsideIt()
    {
        await Driver.VerticalSlice().OpenDialog().TapAsync();

        await Driver.KeyAsync("Tab", repeat: 5);

        await Driver.Dialog().Panel().Expect().ToBeFocusedAsync();
    }

    [TestMethod]
    public async Task AMenuChoiceIsReported()
    {
        await Driver.VerticalSlice().MenuButton().TapAsync();
        await Driver.Menu().Item().WithLabel("Copy").TapAsync();

        await Driver.Menu().Item().Expect().ToBeGoneAsync();
        await SeeAsync(Text("Last menu choice: Copy"));
    }

    [TestMethod]
    public async Task AMenuIsWorkedByTheKeyboard()
    {
        await Driver.VerticalSlice().MenuButton().TapAsync();

        await Driver.KeyAsync("Down");
        await Driver.KeyAsync("Enter");

        await SeeAsync(Text("Last menu choice: Paste"));
    }

    [TestMethod]
    public async Task ADisabledMenuItemDoesNothing()
    {
        await Driver.VerticalSlice().MenuButton().TapAsync();

        await Driver.Menu().Item().WithLabel("Unavailable").TapAsync(force: true);

        await Driver.Menu().Item().WithLabel("Unavailable").Expect().ToBeDisabledAsync();
        await Text("Last menu choice: nothing yet").Expect().ToExistAsync();
    }

    [TestMethod]
    public async Task TheContextMenuOpensOnARightClick()
    {
        await Text("Right-click here").RightClickAsync();

        await Driver.Role.MenuItem("Paste").TapAsync();

        await SeeAsync(Text("Last menu choice: Paste"));
    }

    [TestMethod]
    public async Task ATooltipShowsOnHover()
    {
        await Driver.VerticalSlice().AboutTooltips().HoverAsync();

        await Driver.Role.Tooltip("Tooltips wait 600 ms").Expect().ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task TabsSelectByPressAndByArrow()
    {
        var tabs = Driver.Tabs().TabButton();
        await tabs.WithLabel("Overview").Expect().ToBeSelectedAsync();

        await tabs.WithLabel("Activity").TapAsync();
        await tabs.WithLabel("Activity").Expect().ToBeSelectedAsync();

        await Driver.KeyAsync("Right");
        await tabs.WithLabel("Settings").Expect().ToBeSelectedAsync();
        await tabs.WithLabel("Overview").Expect().ToBeUnselectedAsync();
    }

    [TestMethod]
    public async Task FilterChipsToggle()
    {
        await Driver.Role.CheckBox("Open").Expect().ToBeCheckedAsync();

        await Driver.Role.CheckBox("Open").TapAsync();
        await Driver.Role.CheckBox("Closed").TapAsync();

        await Driver.Role.CheckBox("Open").Expect().ToBeUncheckedAsync();
        await Driver.Role.CheckBox("Closed").Expect().ToBeCheckedAsync();
    }

    [TestMethod]
    public async Task SlidersMoveByKeyAndShowTheirValue()
    {
        await Driver.VerticalSlice().Volume().FocusAsync();

        await Driver.KeyAsync("PageUp");

        await SeeAsync(Text("Volume 50%"));
        await Driver.VerticalSlice().Stepped().Expect().ToHaveValueAsync("0.5");
    }

    [TestMethod]
    public async Task TheSteppedSliderSnaps()
    {
        await Driver.VerticalSlice().Stepped().FocusAsync();

        await Driver.KeyAsync("Right");

        // From 0.4, the next step is 0.5, not a whole step on at 0.65 snapped to 0.75.
        await Driver.VerticalSlice().Stepped().Expect().ToHaveValueAsync("0.5");
        await SeeAsync(Text("Volume 50%"));
    }

    [TestMethod]
    public async Task TextFieldsTakeTyping()
    {
        var slice = Driver.VerticalSlice();

        await slice.Name().TypeAsync("Ada Lovelace");
        await slice.Email().FillAsync("ada@example.com");

        await slice.Name().Expect().ToHaveValueAsync("Ada Lovelace");
        await slice.Email().Expect().ToHaveValueAsync("ada@example.com");
        await slice.Email().Input().Expect().ToBeFocusedAsync();
    }

    [TestMethod]
    public async Task AFieldWithAnErrorSaysSo()
    {
        await SeeAsync(Text("That code has expired"));
    }

    [TestMethod]
    public async Task AFieldKeepsToItsMaximumLength()
    {
        // A tap puts the caret where it lands, as a person's would: End takes it past "12345".
        await Driver.VerticalSlice().Code().Input().TapAsync();
        await Driver.KeyAsync("End");
        await Driver.TypeAsync("6789");

        await Driver.VerticalSlice().Code().Expect().ToHaveValueAsync("123456");
    }

    [TestMethod]
    public async Task TheComboBoxOffersAndTakesAChoice()
    {
        var city = Driver.VerticalSlice().City();
        await city.Expect().ToHaveValueAsync("London");

        await city.FillAsync("Pa");
        await Driver.Role.ListItem("Paris").TapAsync();

        await city.Expect().ToHaveValueAsync("Paris");
        await Driver.Role.ListItem("Paris").Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task TheCalendarChoosesADay()
    {
        var days = Driver.Calendar().DayButton();
        var fifteenth = days.WithLabel(TextMatch.Contains(" 15 "));

        await fifteenth.First().TapAsync();

        await fifteenth.First().Expect().ToBeSelectedAsync();
    }

    [TestMethod]
    public async Task TheCalendarPagesByMonth()
    {
        var heading = await Driver.Calendar().InspectAsync("text", depth: 3);

        await Driver.Calendar().NextMonth().TapAsync();

        var next = await Driver.Calendar().InspectAsync("text", depth: 3);
        Assert.AreNotEqual(Flatten(heading), Flatten(next));
    }

    [TestMethod]
    public async Task TheDatePickerOpensItsCalendar()
    {
        await Driver.VerticalSlice().StartDate().Field().Input().FocusAsync();

        await Driver.KeyAsync("Down");

        await Driver.Role.Dialog("Start date").Expect().ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task ToggleGroupsToggleOneOrMany()
    {
        var bold = Driver.ToggleGroup().Item().WithLabel("Bold");
        var italic = Driver.ToggleGroup().Item().WithLabel("Italic");
        var left = Driver.ToggleGroup().Item().WithLabel("Left");
        var centre = Driver.ToggleGroup().Item().WithLabel("Centre");
        await bold.Expect().ToBeCheckedAsync();

        await italic.TapAsync();
        await centre.TapAsync();

        await bold.Expect().ToBeCheckedAsync();
        await italic.Expect().ToBeCheckedAsync();
        await centre.Expect().ToBeCheckedAsync();
        await left.Expect().ToBeUncheckedAsync();
    }

    [TestMethod]
    public async Task TheSplitButtonOffersItsAlternatives()
    {
        await Driver.VerticalSlice().Save().More().TapAsync();

        await Driver.Role.MenuItem("Save all").Expect().ToBeVisibleAsync();
        await Driver.Role.MenuItem("Save as…").TapAsync();
        await Driver.Role.MenuItem("Save all").Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task NumberFieldsStepWithinTheirRange()
    {
        var quantity = Driver.VerticalSlice().Quantity();

        await quantity.Increase().TapAsync();
        await quantity.Field().Expect().ToHaveValueAsync("3");
        await quantity.Decrease().TapAsync();
        await quantity.Decrease().TapAsync();
        await quantity.Decrease().TapAsync();

        await quantity.Field().Expect().ToHaveValueAsync("0");
        await quantity.Decrease().Expect().ToBeDisabledAsync();
    }

    [TestMethod]
    public async Task ARangeSlidersEndsMoveByKey()
    {
        await Driver.Role.Slider().WithLabel(TextMatch.Contains("Price")).First().FocusAsync();

        await Driver.KeyAsync("Right");

        await SeeAsync(Text("Price $55 to $250"));
    }

    [TestMethod]
    public async Task TheSearchFieldClears()
    {
        var search = Driver.VerticalSlice().SearchComponents();

        await search.TypeAsync("slider");
        await search.Clear().TapAsync();

        await search.Expect().ToHaveValueAsync("");
        await search.Clear().Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task PaginationPages()
    {
        var pages = Driver.Pagination();
        await pages.PageButton().WithLabel(TextMatch.Contains("7")).Expect().ToBeSelectedAsync();

        await pages.Next().TapAsync();

        await pages.PageButton().WithLabel(TextMatch.Contains("8")).Expect().ToBeSelectedAsync();
        await pages.Previous().TapAsync();
        await pages.PageButton().WithLabel(TextMatch.Contains("7")).Expect().ToBeSelectedAsync();
    }

    [TestMethod]
    public async Task TheSheetOpensTakesFiltersAndCloses()
    {
        var slice = Driver.VerticalSlice();
        await slice.OpenSheet().TapAsync();
        await slice.InStock().Expect().ToBeVisibleAsync();

        await slice.OnSale().TapAsync();
        await slice.Apply().TapAsync();

        await slice.InStock().Expect().ToBeHiddenAsync();
        await Driver.Role.CheckBox("Closed").Expect().ToBeCheckedAsync();
    }

    [TestMethod]
    public async Task TheSheetClosesFromItsCloseButton()
    {
        await Driver.VerticalSlice().OpenSheet().TapAsync();

        await Driver.Sheet().Close().TapAsync();

        await Driver.VerticalSlice().InStock().Expect().ToBeHiddenAsync();
    }

    [TestMethod]
    public async Task ThePopoverOpensAndClosesByItsButtonAndEscape()
    {
        await Driver.VerticalSlice().PopoverButton().TapAsync();
        await Text("Popovers hold any content").Expect().ToBeVisibleAsync();
        await Driver.VerticalSlice().GotIt().TapAsync();
        await Text("Popovers hold any content").Expect().ToBeGoneAsync();

        await Driver.VerticalSlice().PopoverButton().TapAsync();
        await Driver.KeyAsync("Escape");
        await Text("Popovers hold any content").Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task TheAlertDialogConfirms()
    {
        await Driver.VerticalSlice().OpenDeleteDialog().TapAsync();
        await Driver.AlertDialog().Confirm().Expect().ToHaveTextAsync("Delete");

        await Driver.AlertDialog().Confirm().TapAsync();

        await Driver.AlertDialog().Confirm().Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task TheAlertDialogCancels()
    {
        await Driver.VerticalSlice().OpenDeleteDialog().TapAsync();

        await Driver.AlertDialog().Cancel().TapAsync();

        await Driver.AlertDialog().Cancel().Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task TheAccordionOpensASection()
    {
        await SeeAsync(Text("Two to four days, tracked."));

        await Driver.Role.Button("Returns").TapAsync();

        await SeeAsync(Text("Free within thirty days."));
    }

    [TestMethod]
    public async Task BreadcrumbsLinkBack()
    {
        await Driver.Breadcrumb().Crumb().WithLabel("Projects").TapAsync();

        await Driver.Breadcrumb().Crumb().Expect().ToExistAsync();
    }

    [TestMethod]
    public async Task TheTrialAlertCanBeDismissedOrUpgraded()
    {
        await Driver.VerticalSlice().Upgrade().TapAsync();

        await Driver.Alert().Dismiss().TapAsync();
    }

    private static string Flatten(Radiant.UI.Automation.InspectNode node) =>
        string.Join("|", (node.Children ?? []).Select(Flatten).Prepend(node.Text ?? node.Label ?? ""));
}
