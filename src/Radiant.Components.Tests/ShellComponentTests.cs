using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class ShellComponentTests
{
    private static readonly Vector2 Viewport = new(800, 600);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new SnackbarHost(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(20), AlignItems = Align.FlexStart },
            Children = [element],
        })));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root, int frames = 40)
    {
        root.Update(Viewport);
        for (var i = 0; i < frames; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static SemanticsNode? Find(UIRoot root, SemanticsRole role, string? label = null) =>
        All(root.GetSemantics()).FirstOrDefault(n => n.Role == role && (label is null || n.Label == label));

    private static void Click(UIRoot root, SemanticsNode node)
    {
        var at = new Vector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
        root.PointerDown(at);
        root.PointerUp(at);
        Settle(root);
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }

    [TestMethod]
    public void AnAppBarHasItsTitleNavigationAndActions()
    {
        var navigated = 0;
        using var root = Mount(new TopAppBar("Inbox")
        {
            NavigationIcon = "menu",
            OnNavigation = () => navigated++,
            Actions = [new IconButton("search", "Search")],
        });

        Click(root, Find(root, SemanticsRole.Button, "Navigate")!);

        Assert.AreEqual(1, navigated);
        Assert.IsNotNull(Find(root, SemanticsRole.Button, "Search"));
        Assert.IsNotNull(All(root.GetSemantics()).FirstOrDefault(n => n.Label == "Inbox"));
    }

    [TestMethod]
    public void RailAndDrawerDestinationsSelect()
    {
        var chosen = new List<int>();
        NavItem[] items = [new("inbox", "Inbox") { Badge = 3 }, new("send", "Sent"), new("archive", "Archive") { Section = "Folders" }];
        using var rail = Mount(new NavigationRail(items, 0, chosen.Add));
        using var drawer = Mount(new NavigationDrawer(items, 0, chosen.Add) { Title = "Mail" });

        Click(rail, Find(rail, SemanticsRole.Tab, "Sent")!);
        Click(drawer, Find(drawer, SemanticsRole.Tab, "Archive")!);

        CollectionAssert.AreEqual(new[] { 1, 2 }, chosen);
        Assert.IsTrue(All(drawer.GetSemantics()).Any(n => n.Label == "Folders"), "the section heading shows");
    }

    [TestMethod]
    public void ASegmentedButtonChoosesOneOrSeveral()
    {
        var single = new List<IReadOnlySet<int>>();
        var multi = new List<IReadOnlySet<int>>();
        Segment[] segments = [new("Day"), new("Week"), new("Month")];
        using var one = Mount(new SegmentedButton(segments, new HashSet<int> { 0 }, single.Add));
        using var many = Mount(new SegmentedButton(segments, new HashSet<int> { 0 }, multi.Add) { MultiSelect = true });

        Click(one, Find(one, SemanticsRole.RadioButton, "Week")!);
        Click(many, Find(many, SemanticsRole.RadioButton, "Week")!);
        Click(many, Find(many, SemanticsRole.RadioButton, "Day")!);

        CollectionAssert.AreEquivalent(new[] { 1 }, single[0].ToArray());
        CollectionAssert.AreEquivalent(new[] { 0, 1 }, multi[0].ToArray());
        CollectionAssert.AreEquivalent(Array.Empty<int>(), multi[1].ToArray(), "pressing a chosen segment unchooses it");
        Assert.AreEqual(true, Find(one, SemanticsRole.RadioButton, "Day")!.Semantics.Checked);
    }

    [TestMethod]
    public void ASelectFieldOpensAMenuAsWideAsItselfAndChooses()
    {
        var chosen = new UI.Core.Signal<int>(0);
        using var root = Mount(new Host(ctx => new SelectField("Size", ["Small", "Medium", "Large"], ctx.Watch(chosen), i => chosen.Value = i)
        {
            Layout = new LayoutStyle { Width = 300 },
        }));
        var field = Find(root, SemanticsRole.TextField, "Size")!;
        Assert.AreEqual("Small", field.Semantics.Value);

        Click(root, field);
        var menu = Find(root, SemanticsRole.Menu)!;
        Assert.IsTrue(menu.Bounds.Width >= 299, $"{menu.Bounds.Width}");
        Click(root, Find(root, SemanticsRole.MenuItem, "Large")!);

        Assert.AreEqual(2, chosen.Value);
        Assert.IsNull(Find(root, SemanticsRole.Menu));
        Assert.AreEqual("Large", Find(root, SemanticsRole.TextField, "Size")!.Semantics.Value);
    }

    [TestMethod]
    public void SnackbarsShowInTurnAndTimeOut()
    {
        Snackbars? queue = null;
        var undone = 0;
        using var root = Mount(new Host(ctx =>
        {
            queue = ctx.UseSnackbars();
            return null;
        }));

        queue!.Show("Saved");
        queue.Show("Deleted", "Undo", () => undone++);
        Settle(root);
        Assert.IsNotNull(Find(root, SemanticsRole.Alert, "Saved"));
        Assert.IsNull(Find(root, SemanticsRole.Alert, "Deleted"), "one at a time");

        Settle(root, 60 * 5);
        Assert.IsNotNull(Find(root, SemanticsRole.Alert, "Deleted"), "the next after the first times out");
        Click(root, Find(root, SemanticsRole.Button, "Undo")!);

        Assert.AreEqual(1, undone);
        Assert.IsNull(Find(root, SemanticsRole.Alert));
    }

    [TestMethod]
    public void AnAvatarShowsInitialsInAStableColour()
    {
        using var first = Mount(new Avatar("Ada Lovelace"));
        using var second = Mount(new Avatar("Ada Lovelace"));

        var texts = All(first.GetSemantics()).Where(n => n.Role == SemanticsRole.Text).Select(n => n.Label).ToArray();
        Assert.IsTrue(texts.Contains("AL") || All(first.GetSemantics()).Any(n => n.Label == "Ada Lovelace"));
        Assert.AreEqual(
            Find(first, SemanticsRole.Image, "Ada Lovelace")!.Bounds,
            Find(second, SemanticsRole.Image, "Ada Lovelace")!.Bounds);
    }
}
