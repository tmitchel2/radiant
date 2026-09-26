using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Components;
using Radiant.UI.Core;
using static Radiant.Templates.Tests.TemplateHarness;

namespace Radiant.Templates.Tests;

[TestClass]
public class ApplicationTemplateTests
{
    [TestMethod]
    public void SigningInReportsTheEnteredDetails()
    {
        var signedIn = new List<(string, string, bool)>();
        using var root = Mount(new SignInForm((email, password, remember) => signedIn.Add((email, password, remember))));

        Type(root, Find(root, SemanticsRole.TextField, "Email"), "ada@example.com");
        Type(root, Find(root, SemanticsRole.TextField, "Password"), "engine");
        Click(root, Find(root, SemanticsRole.Button, "Sign in"));

        CollectionAssert.AreEqual(new[] { ("ada@example.com", "engine", true) }, signedIn);
    }

    [TestMethod]
    public void ThePasswordIsHiddenUntilRevealed()
    {
        using var root = Mount(new SignInForm((_, _, _) => { }));
        Type(root, Find(root, SemanticsRole.TextField, "Password"), "engine");
        var hidden = Find(root, SemanticsRole.TextField, "Password").Semantics.Value;

        Click(root, Find(root, SemanticsRole.Button, "Show password"));

        Assert.AreEqual("••••••", hidden);
        Assert.AreEqual("engine", Find(root, SemanticsRole.TextField, "Password").Semantics.Value);
        Assert.IsTrue(All(root).Any(n => n.Label == "Hide password"));
    }

    [TestMethod]
    public void EditingTheHiddenPasswordEditsTheRealOne()
    {
        var signedIn = new List<string>();
        using var root = Mount(new SignInForm((_, password, _) => signedIn.Add(password)));
        Type(root, Find(root, SemanticsRole.TextField, "Password"), "engin");

        root.TextInput("e");
        root.KeyDown(KeyCode.Enter);
        Settle(root);

        CollectionAssert.AreEqual(new[] { "engine" }, signedIn);
    }

    [TestMethod]
    public void StatsShowTheirChangeSignedAsAPercentage()
    {
        using var root = Mount(new StatsGrid([
            new Stat("Revenue", "$48,210") { Change = 0.124 },
            new Stat("Refunds", "37") { Change = -0.18 },
            new Stat("Visitors", "92.4k"),
        ]));

        Assert.IsTrue(Shows(root, "+12.4%"));
        Assert.IsTrue(Shows(root, "-18%"));
        Assert.IsTrue(Shows(root, "92.4k"));
    }

    [TestMethod]
    public void AStackedListReportsWhichEntryWasPressed()
    {
        var pressed = new List<int>();
        using var root = Mount(new StackedList([new ListEntry("Ada Lovelace", "Paid"), new ListEntry("Grace Hopper", "Refunded") { Meta = "1h ago" }])
        {
            Title = "Recent activity",
            OnPress = pressed.Add,
        });

        Click(root, All(root).First(n => n.Label == "Grace Hopper"));

        CollectionAssert.AreEqual(new[] { 1 }, pressed);
        Assert.IsTrue(Shows(root, "Recent activity"));
    }

    [TestMethod]
    public void SettingsRowsSitInTheirSection()
    {
        var on = false;
        using var root = Mount(new SettingsSection("Appearance", [new SettingsRow("Dark theme", new Switch(on, v => on = v)) { Description = "Use dark colours" }])
        {
            Description = "How it looks",
        });

        Click(root, All(root).First(n => n.Role == SemanticsRole.Switch));

        Assert.IsTrue(on);
        Assert.IsTrue(Shows(root, "Dark theme") && Shows(root, "How it looks"));
    }

    [TestMethod]
    public void AnEmptyStateOffersItsAction()
    {
        var composed = 0;
        using var root = Mount(new EmptyState("inbox", "No messages yet")
        {
            Description = "They will appear here.",
            Action = new SurfaceButton("Compose") { OnPress = () => composed++ },
        });

        Click(root, Find(root, SemanticsRole.Button, "Compose"));

        Assert.AreEqual(1, composed);
        Assert.IsTrue(Shows(root, "No messages yet"));
    }

    [TestMethod]
    public void TheSidebarLayoutTitlesThePageAndNavigates()
    {
        var chosen = new List<int>();
        NavItem[] items = [new("dashboard", "Dashboard"), new("settings", "Settings")];
        using var root = Mount(new SidebarLayout("App", items, 1, chosen.Add, new EmptyState("inbox", "Nothing")));

        Click(root, Find(root, SemanticsRole.Tab, "Dashboard"));

        CollectionAssert.AreEqual(new[] { 0 }, chosen);
        Assert.IsTrue(All(root).Count(n => n.Label == "Settings") >= 2, "the app bar shows the chosen page's name");
    }

    [TestMethod]
    public void AnIdleSidebarLayoutAsksForNoFrames()
    {
        NavItem[] items = [new("dashboard", "Dashboard")];
        using var root = Mount(new SidebarLayout("App", items, 0, _ => { }, new EmptyState("inbox", "Nothing")));

        Assert.IsFalse(root.NeedsUpdate);
    }
}
