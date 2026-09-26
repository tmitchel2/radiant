using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class DatePickerTests
{
    private static readonly Vector2 Viewport = new(700, 700);

    // British: day first, weeks starting on Monday.
    private static readonly CultureInfo British = CultureInfo.GetCultureInfo("en-GB");

    private static readonly DateOnly Today = new(2026, 9, 26);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(20), AlignItems = Align.FlexStart },
            Children = [element, new Box { Focusable = true, Semantics = new Semantics { Role = SemanticsRole.Button, Label = "Elsewhere" }, Layout = new LayoutStyle { Width = 40, Height = 40, Margin = new Edges(0, 500, 0, 0) } }],
        }));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root)
    {
        for (var i = 0; i < 20; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static IEnumerable<SemanticsNode> All(UIRoot root) => All(root.GetSemantics());

    private static SemanticsNode Day(UIRoot root, DateOnly day) => All(root).Single(n => n.Role == SemanticsRole.Button && n.Label == day.ToString("D", British));

    private static SemanticsNode Field(UIRoot root) => All(root).Single(n => n.Role == SemanticsRole.TextField);

    private static void Click(UIRoot root, SemanticsNode node)
    {
        var at = new Vector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
        root.PointerDown(at);
        root.PointerUp(at);
        Settle(root);
    }

    private static void Press(UIRoot root, KeyCode key)
    {
        root.KeyDown(key);
        Settle(root);
    }

    private static Element CalendarOf(Signal<DateOnly?> value, Func<DateOnly, bool>? disabled = null) => new Host(ctx =>
        new Calendar(ctx.Watch(value), d => value.Value = d) { Today = Today, Culture = British, IsDisabled = disabled, Min = new DateOnly(2026, 9, 3) });

    [TestMethod]
    public void TheMonthStartsOnTheCulturesFirstWeekday()
    {
        using var root = Mount(CalendarOf(new Signal<DateOnly?>(null)));

        var first = Day(root, new DateOnly(2026, 9, 1)).Bounds;
        var monday = Day(root, new DateOnly(2026, 9, 7)).Bounds;

        Assert.IsTrue(All(root).Any(n => n.Label == "September 2026"));
        Assert.AreEqual(monday.X + 40, first.X, 0.5f, "1 September 2026 is a Tuesday, the second column");
        Assert.IsTrue(first.Y < monday.Y);
    }

    [TestMethod]
    public void PressingADayChoosesIt()
    {
        var value = new Signal<DateOnly?>(null);
        using var root = Mount(CalendarOf(value));

        Click(root, Day(root, new DateOnly(2026, 9, 15)));

        Assert.AreEqual(new DateOnly(2026, 9, 15), value.Value);
        Assert.IsTrue(Day(root, new DateOnly(2026, 9, 15)).Semantics.Selected);
    }

    [TestMethod]
    public void DaysOutsideTheLimitsCantBeChosen()
    {
        var value = new Signal<DateOnly?>(null);
        using var root = Mount(CalendarOf(value, d => d.DayOfWeek == DayOfWeek.Sunday));

        Click(root, Day(root, new DateOnly(2026, 9, 2)));
        Click(root, Day(root, new DateOnly(2026, 9, 13)));

        Assert.IsNull(value.Value, "before the minimum, and a Sunday");
        Assert.IsTrue(Day(root, new DateOnly(2026, 9, 13)).Semantics.Disabled);
    }

    [TestMethod]
    public void ArrowsMoveByDaysAndWeeksAcrossMonths()
    {
        var value = new Signal<DateOnly?>(new DateOnly(2026, 9, 28));
        using var root = Mount(CalendarOf(value));
        Click(root, Day(root, new DateOnly(2026, 9, 28)));

        Press(root, KeyCode.Down);
        var october = All(root).Any(n => n.Label == "October 2026");
        var focused = Day(root, new DateOnly(2026, 10, 5)).IsFocused;
        Press(root, KeyCode.End);
        Press(root, KeyCode.Enter);

        Assert.IsTrue(october, "a week on from 28 September turned the page");
        Assert.IsTrue(focused);
        Assert.AreEqual(new DateOnly(2026, 10, 11), value.Value, "End went to Sunday, the week's last day");
    }

    [TestMethod]
    public void OnlyTheFocusedDayIsATabStop()
    {
        using var root = Mount(CalendarOf(new Signal<DateOnly?>(new DateOnly(2026, 9, 10))));

        root.KeyDown(KeyCode.Tab);
        root.KeyDown(KeyCode.Tab);
        root.KeyDown(KeyCode.Tab);
        Settle(root);
        var third = All(root).Single(n => n.IsFocused).Label;
        root.KeyDown(KeyCode.Tab);
        Settle(root);

        Assert.AreEqual(new DateOnly(2026, 9, 10).ToString("D", British), third, "previous month, next month, then the chosen day");
        Assert.AreEqual("Elsewhere", All(root).Single(n => n.IsFocused).Label);
    }

    private static Element Picker(Signal<DateOnly?> value) => new Host(ctx =>
        new DatePicker("Start", ctx.Watch(value), d => value.Value = d) { Today = Today, Culture = British, Min = new DateOnly(2026, 1, 1) });

    [TestMethod]
    public void TypedDatesAreReadInTheCulturesFormat()
    {
        var value = new Signal<DateOnly?>(null);
        using var root = Mount(Picker(value));
        Click(root, Field(root));

        root.TextInput("03/10/2026");
        Press(root, KeyCode.Enter);

        Assert.AreEqual(new DateOnly(2026, 10, 3), value.Value, "day first");
        Assert.AreEqual("03/10/2026", Field(root).Semantics.Value);
    }

    [TestMethod]
    public void TextThatIsntAChoosableDateShowsAnError()
    {
        var value = new Signal<DateOnly?>(new DateOnly(2026, 5, 1));
        using var root = Mount(Picker(value));
        Click(root, Field(root));

        root.TextInput("x");
        Click(root, All(root).Single(n => n.Label == "Elsewhere"));
        var bad = All(root).Any(n => n.Label?.StartsWith("Enter a date like 31/12/2026", StringComparison.Ordinal) == true);

        Assert.IsTrue(bad);
        Assert.AreEqual(new DateOnly(2026, 5, 1), value.Value, "the value stands");
    }

    [TestMethod]
    public void TheCalendarOpensOnTheDateAndPickingClosesIt()
    {
        var value = new Signal<DateOnly?>(new DateOnly(2026, 3, 14));
        using var root = Mount(Picker(value));

        Click(root, All(root).Single(n => n.Label == "Choose Start"));
        var focused = Day(root, new DateOnly(2026, 3, 14)).IsFocused;
        Press(root, KeyCode.Right);
        Press(root, KeyCode.Enter);

        Assert.IsTrue(focused, "the chosen day takes focus");
        Assert.AreEqual(new DateOnly(2026, 3, 15), value.Value);
        Assert.IsFalse(All(root).Any(n => n.Role == SemanticsRole.Dialog), "picking closed it");
        Assert.IsTrue(Field(root).IsFocused, "focus is back in the field");
    }

    [TestMethod]
    public void EscapeClosesTheCalendarWithoutChanging()
    {
        var value = new Signal<DateOnly?>(new DateOnly(2026, 3, 14));
        using var root = Mount(Picker(value));
        Click(root, Field(root));

        Press(root, KeyCode.Down);
        var opened = All(root).Any(n => n.Role == SemanticsRole.Dialog);
        Press(root, KeyCode.Escape);

        Assert.IsTrue(opened, "Down opens it");
        Assert.IsFalse(All(root).Any(n => n.Role == SemanticsRole.Dialog));
        Assert.AreEqual(new DateOnly(2026, 3, 14), value.Value);
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
