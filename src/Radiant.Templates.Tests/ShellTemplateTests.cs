using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Components;
using Radiant.UI.Core;
using static Radiant.Templates.Tests.TemplateHarness;

namespace Radiant.Templates.Tests;

[TestClass]
public class ShellTemplateTests
{
    private static readonly NavItem[] s_activities = [new("folder", "Explorer"), new("search", "Search")];

    private static WorkspaceLayout Workspace(int activity, List<int>? chosen = null) =>
        new(s_activities, activity, chosen is null ? null : chosen.Add, new SurfaceText("editor"))
        {
            Sidebar = new SurfaceText("files"),
            Panel = new SurfaceText("terminal"),
            PanelTitle = "Terminal",
            Inspector = new SurfaceText("properties"),
            StatusBar = new StatusBar { Leading = [new StatusItem("main")] },
        };

    [TestMethod]
    public void AWorkspaceDocksEveryPartBehindSplitters()
    {
        using var root = Mount(Workspace(0));

        Assert.IsTrue(new[] { "editor", "files", "terminal", "properties", "main", "Explorer", "Terminal" }.All(t => Shows(root, t)));
        CollectionAssert.AreEquivalent(new[] { "Side bar", "Terminal", "Inspector" },
            All(root).Where(n => n.Role == SemanticsRole.Separator).Select(n => n.Label).ToArray());
    }

    [TestMethod]
    public void TheActivityBarChoosesWhatTheSideBarShows()
    {
        var chosen = new List<int>();
        using var root = Mount(Workspace(0, chosen));

        Click(root, Find(root, SemanticsRole.Tab, "Search"));

        CollectionAssert.AreEqual(new[] { 1 }, chosen);
        Assert.IsTrue(Find(root, SemanticsRole.Tab, "Explorer").Semantics.Selected);
    }

    [TestMethod]
    public void WithNoActivityTheSideBarAndItsSplitterGo()
    {
        using var root = Mount(Workspace(-1) with { Panel = null, Inspector = null });

        Assert.IsFalse(Shows(root, "files"));
        Assert.IsFalse(All(root).Any(n => n.Role == SemanticsRole.Separator));
        Assert.IsTrue(Shows(root, "editor"));
    }

    private static readonly ListEntry[] s_messages =
    [
        new("Ada Lovelace", "Analytical Engine"),
        new("Grace Hopper", "Compilers"),
        new("Alan Turing", "Computable numbers"),
    ];

    private static Element Mail(Signal<int> selected) => new Host(context =>
    {
        var index = context.Watch(selected);
        return new MasterDetail(s_messages, index, i => selected.Value = i, index < 0 ? null : new SurfaceText($"Reading {s_messages[index].Title}"))
        {
            Title = "Inbox",
        };
    });

    [TestMethod]
    public void ChoosingAnItemShowsItsDetail()
    {
        var selected = new Signal<int>(-1);
        using var root = Mount(Mail(selected));
        var empty = Shows(root, "Nothing selected");

        Click(root, Find(root, SemanticsRole.ListItem, "Grace Hopper"));

        Assert.IsTrue(empty);
        Assert.AreEqual(1, selected.Value);
        Assert.IsTrue(Shows(root, "Reading Grace Hopper"));
        Assert.IsTrue(Find(root, SemanticsRole.ListItem, "Grace Hopper").Semantics.Selected);
    }

    [TestMethod]
    public void SearchingNarrowsTheListAndKeepsIndicesInTheFullList()
    {
        var selected = new Signal<int>(-1);
        using var root = Mount(Mail(selected));

        Type(root, Find(root, SemanticsRole.TextField, "Search"), "turing");
        var shown = All(root).Where(n => n.Role == SemanticsRole.ListItem).Select(n => n.Label).ToArray();
        Click(root, Find(root, SemanticsRole.ListItem, "Alan Turing"));

        CollectionAssert.AreEqual(new[] { "Alan Turing" }, shown);
        Assert.AreEqual(2, selected.Value);
    }

    [TestMethod]
    public void ASearchWithNoMatchesSaysSo()
    {
        using var root = Mount(Mail(new Signal<int>(-1)));

        Type(root, Find(root, SemanticsRole.TextField, "Search"), "zzz");

        Assert.IsTrue(Shows(root, "No matches"));
    }

    [TestMethod]
    public void UpAndDownMoveThroughTheList()
    {
        var selected = new Signal<int>(-1);
        using var root = Mount(Mail(selected));
        Click(root, Find(root, SemanticsRole.ListItem, "Ada Lovelace"));

        root.KeyDown(KeyCode.Down);
        Settle(root);
        root.KeyDown(KeyCode.Down);
        Settle(root);
        root.KeyDown(KeyCode.Up);
        Settle(root);

        Assert.AreEqual(1, selected.Value);
    }

    private static Element Wizard(Signal<int> step, Signal<bool> named, List<string> events) => new Host(context =>
    {
        var current = context.Watch(step);
        var ready = context.Watch(named);
        return new Wizard(
            [
                new WizardStep("Template", new SurfaceText("pick one")),
                new WizardStep("Name", new SurfaceText("name it")) { CanContinue = ready },
                new WizardStep("Review", new SurfaceText("check it")),
            ],
            current,
            s => step.Value = s)
        {
            Title = "New project",
            FinishText = "Create",
            OnFinish = () => events.Add("finish"),
            OnCancel = () => events.Add("cancel"),
        };
    });

    [TestMethod]
    public void AWizardStepsForwardAndBack()
    {
        var step = new Signal<int>(0);
        using var root = Mount(Wizard(step, new Signal<bool>(true), []));
        Click(root, Find(root, SemanticsRole.Button, "Back"));
        var atStart = step.Value;

        Click(root, Find(root, SemanticsRole.Button, "Next"));
        var second = Shows(root, "name it") && Shows(root, "Step 2 of 3");
        Click(root, Find(root, SemanticsRole.Button, "Back"));

        Assert.AreEqual(0, atStart, "Back does nothing on the first step");
        Assert.IsTrue(second);
        Assert.AreEqual(0, step.Value);
        Assert.IsNotNull(TryFind(root, SemanticsRole.Heading, "Template"), "the step's title is a heading");
    }

    [TestMethod]
    public void NextWaitsUntilTheStepCanContinue()
    {
        var step = new Signal<int>(1);
        var named = new Signal<bool>(false);
        using var root = Mount(Wizard(step, named, []));

        Click(root, Find(root, SemanticsRole.Button, "Next"));
        var blocked = step.Value;
        named.Value = true;
        Settle(root);
        Click(root, Find(root, SemanticsRole.Button, "Next"));

        Assert.AreEqual(1, blocked);
        Assert.IsTrue(Find(root, SemanticsRole.Button, "Create") is not null);
        Assert.AreEqual(2, step.Value);
    }

    [TestMethod]
    public void TheLastStepFinishesAndDoneStepsCanBeRevisited()
    {
        var step = new Signal<int>(2);
        var events = new List<string>();
        using var root = Mount(Wizard(step, new Signal<bool>(true), events));

        Click(root, Find(root, SemanticsRole.Button, "Create"));
        Click(root, Find(root, SemanticsRole.Button, "Cancel"));
        Click(root, Find(root, SemanticsRole.ListItem, "Template"));

        CollectionAssert.AreEqual(new[] { "finish", "cancel" }, events);
        Assert.AreEqual(0, step.Value, "a done step is pressed to go back to it");
    }

    [TestMethod]
    public void PreferencesShowTheChosenCategoryUnderItsName()
    {
        var category = new Signal<int>(0);
        NavItem[] categories = [new("settings", "General"), new("palette", "Appearance"), new("person", "Account") { Section = "You" }];
        using var root = Mount(new Host(context =>
        {
            var chosen = context.Watch(category);
            return new PreferencesLayout(categories, chosen, i => category.Value = i, new SurfaceText($"{categories[chosen].Label} settings"));
        }));

        Click(root, Find(root, SemanticsRole.Tab, "Appearance"));
        root.KeyDown(KeyCode.Down);
        Settle(root);

        Assert.AreEqual(2, category.Value);
        Assert.IsTrue(Shows(root, "Account settings"));
        Assert.IsNotNull(TryFind(root, SemanticsRole.Heading, "Account"), "the category's name is a heading");
        Assert.IsTrue(Shows(root, "You"), "section headings divide the list");
    }

    [TestMethod]
    public void AnInspectorSectionFoldsAway()
    {
        using var root = Mount(new InspectorSection("Appearance", [new PropertyRow("Opacity", new SurfaceText("100%"))]));
        var open = Find(root, SemanticsRole.Button, "Appearance").Semantics.Expanded;

        Click(root, Find(root, SemanticsRole.Button, "Appearance"));

        Assert.AreEqual(true, open);
        Assert.AreEqual(false, Find(root, SemanticsRole.Button, "Appearance").Semantics.Expanded);
        Assert.IsFalse(Shows(root, "Opacity"));
    }
}
