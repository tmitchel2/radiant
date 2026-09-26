using Radiant.Host.AgentControlProtocol;
using Radiant.UI.Driver;

namespace Radiant.UI.Automation.Tests.Gallery;

/// <summary>The desktop shells: a workspace, mail, a wizard, preferences and docking.</summary>
[TestClass]
public sealed class ShellPagesTests : GalleryTest
{
    [TestMethod]
    public async Task TheExplorerOpensAFileInATab()
    {
        await GoToAsync("Workspace");

        await Driver.Role.TreeItem("Program.cs").TapAsync();

        await Driver.Role.Tab("Program.cs").Expect().ToBeSelectedAsync();
    }

    [TestMethod]
    public async Task ADocumentTabCloses()
    {
        await GoToAsync("Workspace");
        await Driver.Role.Tab("README.md").Expect().ToExistAsync();

        // A tab's close button shows on the chosen tab, or the one under the pointer.
        await Driver.Role.Tab("README.md").HoverAsync();
        await Driver.Role.Button("Close README.md").TapAsync();

        await Driver.Role.Tab("README.md").Expect().ToBeGoneAsync();
        await Driver.Role.Tab("Counter.cs").Expect().ToBeSelectedAsync();
    }

    [TestMethod]
    public async Task TheExplorerFolderCollapses()
    {
        await GoToAsync("Workspace");
        await Driver.Role.TreeItem("Counter.cs").Expect().ToBeVisibleAsync();

        await Driver.Role.Button("Collapse Hello").TapAsync();

        await Driver.Role.TreeItem("Counter.cs").Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task TheActivityBarSwitchesTheSidebar()
    {
        await GoToAsync("Workspace");

        await Driver.Role.Tab("Search").TapAsync();

        await Text("Nothing here yet.").Expect().ToBeVisibleAsync();
        await Driver.Role.Tree("Explorer").Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task TheInspectorsControlsWork()
    {
        await GoToAsync("Workspace");
        var page = Driver.WorkspacePage();

        await page.VisibleSwitch().TapAsync();
        await page.Corners().Role.RadioButton("Square").TapAsync();

        await page.VisibleSwitch().Expect().ToBeUncheckedAsync();
        await page.Corners().Role.RadioButton("Square").Expect().ToBeCheckedAsync();
        await page.Corners().Role.RadioButton("Round").Expect().ToBeUncheckedAsync();
    }

    [TestMethod]
    public async Task ChoosingAMessageShowsIt()
    {
        await GoToAsync("Mail");
        await Driver.Role.Heading("Notes on the Analytical Engine").Expect().ToBeVisibleAsync();

        await Driver.Role.ListItem(TextMatch.Contains("Grace Hopper")).TapAsync();

        await Driver.Role.Heading("Re: the compiler, and a moth").Expect().ToBeVisibleAsync();
        await Driver.MailPage().Reply().Expect().ToBeHittableAsync();
    }

    [TestMethod]
    public async Task TheInboxIsSearched()
    {
        await GoToAsync("Mail");

        await Driver.MasterDetail().Search().TypeAsync("turing");

        Assert.AreEqual(1, await Driver.Role.ListItem().WithText(TextMatch.Contains("Alan Turing")).CountAsync());
        await Driver.Role.ListItem(TextMatch.Contains("Ada Lovelace")).Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task TheWizardGoesThroughItsStepsAndFinishes()
    {
        await GoToAsync("New project");
        var wizard = Driver.Wizard();
        var page = Driver.NewProjectPage();
        await wizard.Back().Expect().ToBeDisabledAsync();

        await page.Template().Option().WithLabel("Sidebar app").TapAsync();
        await wizard.Next().TapAsync();
        await page.ProjectName().FillAsync("");
        await wizard.Next().Expect().ToBeDisabledAsync();
        await page.ProjectName().TypeAsync("Notebook");
        await wizard.Next().TapAsync();
        await page.Git().TapAsync();
        await wizard.Next().TapAsync();

        await Text("Sidebar app").Expect().ToBeVisibleAsync();
        await Text("Notebook").Expect().ToBeVisibleAsync();
        await wizard.Next().Expect().ToHaveTextAsync("Create");
        await wizard.Next().TapAsync();
        await page.Template().Option().WithLabel("Empty app").Expect().ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task TheWizardGoesBack()
    {
        await GoToAsync("New project");

        await Driver.Wizard().Next().TapAsync();
        await Driver.Wizard().Back().TapAsync();

        await Driver.NewProjectPage().Template().Expect().ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task PreferencesSwitchCategories()
    {
        await GoToAsync("Preferences");
        await Driver.PreferencesPage().Launch().Expect().ToBeCheckedAsync();

        await Driver.PreferencesPage().Sounds().TapAsync();
        await Driver.PreferencesPage().Sounds().Expect().ToBeCheckedAsync();
        await Driver.Role.Tab("Keyboard").TapAsync();

        await Text("Keyboard settings").Expect().ToBeVisibleAsync();
        await Driver.PreferencesPage().Launch().Expect().ToBeGoneAsync();
    }

    [TestMethod]
    public async Task DockedPanelsSwitchAndClose()
    {
        await GoToAsync("Docking");

        await Driver.Role.Tab("README.md").TapAsync();
        await Text("A counter, built with Radiant.").Expect().ToBeVisibleAsync();

        await Driver.Role.Button("Close Outline").TapAsync();
        await Driver.Role.Tab("Outline").Expect().ToBeGoneAsync();
    }
}
