using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.UI.Core;
using static Radiant.Components.Tests.GoldenSheets;

namespace Radiant.Components.Tests;

/// <summary>Goldens for navigation, collections, overlays and feedback, and a right-to-left sheet.</summary>
[TestClass]
[TestCategory("Gpu")]
public class MoreComponentGoldenTests
{
    private static Vector2 Centre(UIRoot root, SemanticsRole role, string? label = null)
    {
        var node = All(root.GetSemantics()).First(n => n.Role == role && (label is null || n.Label == label));
        return new Vector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
    }

    [TestMethod]
    public void Navigation() => Check("Navigation", 600, 330, () => Column(16,
        new TopAppBar("Inbox")
        {
            NavigationIcon = "menu",
            Actions = [new IconButton("search", "Search"), new IconButton("more_vert", "More")],
        },
        new Box
        {
            Layout = new LayoutStyle { AlignSelf = Align.Stretch },
            Children = [new Tabs([new Tab("Mail") { Icon = "mail" }, new Tab("Chat") { Icon = "chat" }, new Tab("Meet") { Icon = "videocam" }], 1, _ => { })],
        },
        Row(
            new Breadcrumb([new Crumb("Home", () => { }), new Crumb("Projects", () => { }), new Crumb("Radiant")]),
            new Pagination(12, 4, _ => { })),
        new SegmentedButton([new Segment("Day"), new Segment("Week"), new Segment("Month")], new HashSet<int> { 1 }, _ => { })));

    [TestMethod]
    public void NavigationRail() => Check("NavigationRail", 140, 330, () =>
        new NavigationRail([new NavItem("inbox", "Inbox") { Badge = 3 }, new NavItem("send", "Sent"), new NavItem("edit_note", "Drafts")], 0, _ => { }));

    [TestMethod]
    public void Lists() => Check("Lists", 560, 420, () => Column(8,
        new Box
        {
            Layout = new LayoutStyle { AlignSelf = Align.Stretch },
            Children =
            [
                new ListItem("One line") { LeadingIcon = "person", OnPress = () => { } },
                new ListItem("Two lines") { SupportingText = "Supporting text under the headline", LeadingIcon = "mail", TrailingText = "9:41", OnPress = () => { } },
                new ListItem("Selected") { LeadingIcon = "star", TrailingIcon = "chevron_right", Selected = true, OnPress = () => { } },
                new ListItem("Disabled") { LeadingIcon = "block", Disabled = true, OnPress = () => { } },
            ],
        },
        new TreeView(
        [
            new TreeNode("src", "src")
            {
                Icon = "folder",
                ExpandedIcon = "folder_open",
                Children = [new TreeNode("app", "App.cs") { Icon = "code" }, new TreeNode("readme", "README.md") { Icon = "description", Trailing = "M" }],
            },
            new TreeNode("tests", "tests") { Icon = "folder", Children = [new TreeNode("t", "AppTests.cs")] },
        ])
        {
            Selected = "app",
            InitialExpanded = new HashSet<string> { "src" },
            Label = "Files",
        }));

    [TestMethod]
    public void Table()
    {
        string[] names = ["Ada", "Grace", "Linus", "Margaret"];
        string[] roles = ["Engineer", "Admiral", "Maintainer", "Director"];
        int[] commits = [1843, 512, 30211, 977];
        Check("Table", 560, 260, () => new Box
        {
            Layout = new LayoutStyle { AlignSelf = Align.Stretch, Height = 220 },
            Children =
            [
                new DataTable(
                [
                    new DataColumn("Name", i => new SurfaceText(names[i])) { Sortable = true },
                    new DataColumn("Role", i => new SurfaceText(roles[i])) { Grow = true },
                    new DataColumn("Commits", i => new SurfaceText(commits[i].ToString(System.Globalization.CultureInfo.InvariantCulture))) { Width = 110, Alignment = Radiant.Text.TextAlignment.End },
                ], names.Length)
                {
                    SortColumn = 0,
                    ShowCheckboxes = true,
                    MultiSelect = true,
                    Selection = new HashSet<int> { 1 },
                    Label = "People",
                    Layout = new LayoutStyle { FlexGrow = 1 },
                },
            ],
        });
    }

    [TestMethod]
    public void Feedback() => Check("Feedback", 560, 330, () => Column(16,
        Row(
            new CircularProgress { Value = 0.65f, Label = "Sync" },
            new Avatar("Ada Lovelace"),
            new Avatar("Grace Hopper") { Size = 32 },
            new Avatar(null)),
        new Accordion(
        [
            new AccordionItem("Shipping", new SurfaceText("Free over £50.")) { Icon = "local_shipping" },
            new AccordionItem("Returns", new SurfaceText("Within 30 days.")) { Subtitle = "Free", Icon = "undo" },
        ])
        {
            InitiallyOpen = new HashSet<int> { 0 },
            Layout = new LayoutStyle { Width = 400 },
        }));

    [TestMethod]
    public void OpenMenu()
    {
        var anchor = new ElementRef();
        Check("Menu", 360, 300, () => new Box
        {
            Children =
            [
                new Box { Ref = anchor, Children = [new SurfaceButton("Actions", ButtonVariant.Outlined)] },
                new Menu(anchor, true, () => { },
                [
                    new MenuItem("Cut") { Icon = "content_cut", Shortcut = "⌘X" },
                    new MenuItem("Copy") { Icon = "content_copy", Shortcut = "⌘C" },
                    new MenuItem("Paste") { Icon = "content_paste", Shortcut = "⌘V", Disabled = true },
                    new MenuItem("Delete") { Icon = "delete", DividerBefore = true },
                ]),
            ],
        });
    }

    [TestMethod]
    public void MenuFromTheKeyboard()
    {
        // Moved through with the arrows, the focused item shows its ring, inside the item.
        var anchor = new ElementRef();
        Check("MenuKeyboard", 360, 240, () => new Box
        {
            Children =
            [
                new Box { Ref = anchor, Children = [new SurfaceButton("Actions", ButtonVariant.Outlined)] },
                new Menu(anchor, true, () => { }, [new MenuItem("Cut"), new MenuItem("Copy"), new MenuItem("Paste")]),
            ],
        }, act: root => root.KeyDown(KeyCode.Down));
    }

    [TestMethod]
    public void OpenDialog() => Check("Dialog", 520, 360, () => new Dialog(true, () => { })
    {
        Icon = "delete",
        Title = "Delete 3 files?",
        Text = "They'll be moved to the bin, where you can restore them for 30 days.",
        Actions = [new SurfaceButton("Cancel", ButtonVariant.Text), new SurfaceButton("Delete", ButtonVariant.Text)],
    });

    [TestMethod]
    public void HoveredTooltip() => Check("Tooltip", 260, 120, () => new Box
    {
        Layout = new LayoutStyle { Padding = new Edges(60, 50, 0, 0) },
        Children = [new Tooltip("Search (⌘K)", new IconButton("search", "Search"))],
    }, act: root => root.PointerMove(Centre(root, SemanticsRole.Button, "Search")), frames: 60);

    [TestMethod]
    public void RightToLeft() => Check("RightToLeft", 480, 330, () => Column(12,
        new TopAppBar("Inbox") { NavigationIcon = "arrow_back", Actions = [new IconButton("search", "Search")] },
        new Box
        {
            Layout = new LayoutStyle { AlignSelf = Align.Stretch },
            Children = [new Tabs([new Tab("Mail"), new Tab("Chat"), new Tab("Meet")], 0, _ => { })],
        },
        Row(new Switch(true, _ => { }) { Label = "Wi-Fi" }, new Checkbox(true, _ => { }) { Label = "Sync" }),
        new TextField("Name") { InitialText = "Radiant", Layout = new LayoutStyle { Width = 260 } },
        new Box { Layout = new LayoutStyle { Width = 300 }, Children = [new Slider(0.3f, _ => { }) { Label = "Volume" }] }),
        rightToLeft: true);
}
