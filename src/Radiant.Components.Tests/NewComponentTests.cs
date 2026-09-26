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
public class NewComponentTests
{
    private static readonly Vector2 Viewport = new(700, 500);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(16), AlignItems = Align.FlexStart },
            Children = [element],
        }));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root)
    {
        for (var i = 0; i < 5; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static SemanticsNode Find(UIRoot root, Func<SemanticsNode, bool> match) => All(root.GetSemantics()).First(match);

    private static Vector2 Centre(SemanticsNode node) => new(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);

    private static void Click(UIRoot root, SemanticsNode node)
    {
        root.PointerDown(Centre(node));
        root.PointerUp(Centre(node));
        Settle(root);
    }

    private static void Press(UIRoot root, KeyCode key)
    {
        root.KeyDown(key);
        Settle(root);
    }

    [TestMethod]
    public void ATimeIsTypedAndSteppedPartByPart()
    {
        var time = new Signal<TimeOnly?>(null);
        using var root = Mount(new Host(ctx => new TimePicker("Start", ctx.Watch(time), t => time.Value = t) { Use24Hour = true }));
        Assert.AreEqual("––", Find(root, n => n.Label == "Hour").Semantics.Value);

        Click(root, Find(root, n => n.Label == "Hour"));
        Press(root, KeyCode.Number0);
        Press(root, KeyCode.Number9);
        // The second digit finishes the hour and moves on to the minute.
        Press(root, KeyCode.Number4);
        Press(root, KeyCode.Number1);
        Assert.AreEqual(new TimeOnly(9, 41), time.Value);

        Press(root, KeyCode.Up);
        Assert.AreEqual(new TimeOnly(9, 42), time.Value);
        Press(root, KeyCode.Left);
        Press(root, KeyCode.Down);
        Press(root, KeyCode.Down);
        Press(root, KeyCode.Down);
        Press(root, KeyCode.Down);
        Press(root, KeyCode.Down);
        Press(root, KeyCode.Down);
        Press(root, KeyCode.Down);
        Press(root, KeyCode.Down);
        Press(root, KeyCode.Down);
        Press(root, KeyCode.Down);
        Assert.AreEqual(new TimeOnly(23, 42), time.Value, "stepping wraps round midnight");

        Press(root, KeyCode.Backspace);
        Assert.IsNull(time.Value);
    }

    [TestMethod]
    public void TwelveHourTimeHasAnAmPmPart()
    {
        var time = new Signal<TimeOnly?>(new TimeOnly(9, 5));
        using var root = Mount(new Host(ctx => new TimePicker("Alarm", ctx.Watch(time), t => time.Value = t) { Use24Hour = false }));

        Assert.AreEqual("9", Find(root, n => n.Label == "Hour").Semantics.Value);
        Assert.AreEqual("05", Find(root, n => n.Label == "Minute").Semantics.Value);
        Click(root, Find(root, n => n.Label == "AM or PM"));
        Press(root, KeyCode.P);

        Assert.AreEqual(new TimeOnly(21, 5), time.Value);
    }

    [TestMethod]
    public void AButtonGroupsButtonsPressButDisabledOnesDont()
    {
        var pressed = new List<string>();
        using var root = Mount(new ButtonGroup(
        [
            new GroupButton("Bold", () => pressed.Add("bold")) { Icon = "format_bold", IconOnly = true },
            new GroupButton("Italic", () => pressed.Add("italic")) { Icon = "format_italic", IconOnly = true },
            new GroupButton("Strike", () => pressed.Add("strike")) { Disabled = true },
        ]) { Label = "Style" });

        Click(root, Find(root, n => n.Label == "Italic"));
        Click(root, Find(root, n => n.Label == "Strike"));

        CollectionAssert.AreEqual(new[] { "italic" }, pressed);
        Assert.AreEqual(SemanticsRole.Group, Find(root, n => n.Label == "Style").Role);
    }

    [TestMethod]
    public void ACollapsibleShowsItsContentWhenOpen()
    {
        using var root = Mount(new Collapsible("Advanced", new SurfaceText("Hidden settings")));
        var header = Find(root, n => n.Label == "Advanced");
        Assert.AreEqual(false, header.Semantics.Expanded);
        Assert.IsFalse(All(root.GetSemantics()).Any(n => n.Label == "Hidden settings"));

        Click(root, header);

        Assert.AreEqual(true, Find(root, n => n.Label == "Advanced").Semantics.Expanded);
        Assert.IsTrue(All(root.GetSemantics()).Any(n => n.Label == "Hidden settings"));
    }

    [TestMethod]
    public void APropertyGridFiltersByName()
    {
        using var root = Mount(new Box
        {
            Layout = new LayoutStyle { Width = 400 },
            Children =
            [
                new PropertyGrid(
                [
                    new PropertySection("Layout", [new PropertyItem("Width", new SurfaceText("240")), new PropertyItem("Height", new SurfaceText("120"))]),
                    new PropertySection("Appearance", [new PropertyItem("Opacity", new SurfaceText("1.0"))]) { InitiallyOpen = false },
                ]) { Filterable = true },
            ],
        });
        Assert.IsFalse(All(root.GetSemantics()).Any(n => n.Label == "Opacity"), "a closed section hides its rows");

        Click(root, Find(root, n => n.Role == SemanticsRole.TextField));
        foreach (var c in "opa")
        {
            root.TextInput(c.ToString());
            Settle(root);
        }

        var rows = All(root.GetSemantics()).Where(n => n.Role == SemanticsRole.Group && n.Label is "Width" or "Height" or "Opacity").Select(n => n.Label).ToArray();
        CollectionAssert.AreEqual(new[] { "Opacity" }, rows, "only matches show, and their section opens");
    }

    [TestMethod]
    public void AFieldsetIsAGroupNamedByItsLegendAndHiddenTextIsReadOut()
    {
        using var root = Mount(new Fieldset("Notifications", [new Checkbox(true, _ => { }) { Label = "Email" }, new VisuallyHidden("2 unread")]));

        var group = Find(root, n => n.Role == SemanticsRole.Group && n.Label == "Notifications");
        Assert.IsTrue(All(group).Any(n => n.Label == "Email"));
        var hidden = Find(root, n => n.Label == "2 unread");
        Assert.IsTrue(hidden.Bounds.Width <= 1 && hidden.Bounds.Height <= 1);
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
